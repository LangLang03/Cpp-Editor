namespace C__Editor;

internal static class DebuggerExecutableResolver
{
    private static readonly IReadOnlyList<string> KnownCdbPaths = new[]
    {
        @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe",
        @"C:\Program Files\Windows Kits\10\Debuggers\x64\cdb.exe",
        @"C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe",
        @"C:\Program Files\Windows Kits\10\Debuggers\x86\cdb.exe"
    };

    internal static bool TryResolve(
        ToolchainSettingsConfig settings,
        ResolvedToolchainContext toolchain,
        out DebuggerResolution resolution,
        out string detail)
    {
        resolution = new DebuggerResolution();
        detail = string.Empty;

        var requiredKind = ResolveRequiredDebuggerKind(toolchain);
        if (TryResolveAutoDebugger(requiredKind, toolchain, out var autoPath, out var autoFailureDetail))
        {
            resolution = new DebuggerResolution
            {
                Kind = requiredKind,
                ExecutablePath = autoPath,
                Detail = $"使用自动匹配调试器: {requiredKind} -> {autoPath}"
            };

            detail = resolution.Detail;
            return true;
        }

        var customPath = ResolveCustomDebuggerPath(settings);
        if (string.IsNullOrWhiteSpace(customPath))
        {
            detail = string.IsNullOrWhiteSpace(autoFailureDetail)
                ? BuildNotFoundDetail(requiredKind)
                : $"自动匹配到的 {requiredKind} 不可用: {autoFailureDetail}。{BuildNotFoundDetail(requiredKind)}";
            return false;
        }

        if (!File.Exists(customPath))
        {
            detail = $"手动调试器路径不存在: {customPath}";
            return false;
        }

        if (!TryClassifyDebugger(customPath, out var customKind))
        {
            detail = $"无法识别手动调试器类型（仅支持 cdb/gdb/lldb）: {customPath}";
            return false;
        }

        if (customKind != requiredKind)
        {
            detail = $"当前工具链要求调试器为 {requiredKind}，你填写的是 {customKind}。为避免 ABI 和符号不兼容，已拒绝跨类型调试器。";
            return false;
        }

        if (!DebuggerExecutableValidator.TryValidate(requiredKind, customPath, out var customValidationDetail))
        {
            detail = $"手动调试器不可用: {customPath}。{customValidationDetail}";
            return false;
        }

        resolution = new DebuggerResolution
        {
            Kind = requiredKind,
            ExecutablePath = customPath,
            Detail = $"自动匹配未命中，使用手动调试器: {customPath}"
        };

        detail = resolution.Detail;
        return true;
    }

    private static DebuggerKind ResolveRequiredDebuggerKind(ResolvedToolchainContext toolchain)
    {
        return toolchain.Id switch
        {
            ToolchainId.BuiltInMsvc => DebuggerKind.Cdb,
            ToolchainId.LocalMsvc => DebuggerKind.Cdb,
            ToolchainId.BuiltInMinGw => DebuggerKind.Gdb,
            ToolchainId.Gcc => DebuggerKind.Gdb,
            ToolchainId.Gpp => DebuggerKind.Gdb,
            ToolchainId.Clang => DebuggerKind.Lldb,
            ToolchainId.ClangPlusPlus => DebuggerKind.Lldb,
            _ => toolchain.Family == ToolchainFamily.Msvc ? DebuggerKind.Cdb : DebuggerKind.Gdb
        };
    }

    private static bool TryResolveAutoDebugger(
        DebuggerKind kind,
        ResolvedToolchainContext toolchain,
        out string executablePath,
        out string failureDetail)
    {
        executablePath = string.Empty;
        failureDetail = string.Empty;

        var validationFailures = new List<string>();
        foreach (var candidate in ResolveAutoCandidates(kind, toolchain))
        {
            var normalized = NormalizePath(candidate);
            if (string.IsNullOrWhiteSpace(normalized) || !File.Exists(normalized))
            {
                continue;
            }

            if (DebuggerExecutableValidator.TryValidate(kind, normalized, out var validationDetail))
            {
                executablePath = normalized;
                return true;
            }

            if (validationFailures.Count < 2)
            {
                validationFailures.Add($"{normalized} ({validationDetail})");
            }
        }

        if (validationFailures.Count > 0)
        {
            failureDetail = string.Join("；", validationFailures);
        }

        return false;
    }

    private static IReadOnlyList<string> ResolveAutoCandidates(DebuggerKind kind, ResolvedToolchainContext toolchain)
    {
        return kind switch
        {
            DebuggerKind.Cdb => ResolveCdbCandidates(toolchain),
            DebuggerKind.Gdb => ResolveGdbCandidates(toolchain),
            DebuggerKind.Lldb => ResolveLldbCandidates(toolchain),
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyList<string> ResolveCdbCandidates(ResolvedToolchainContext toolchain)
    {
        var candidates = new List<string>();
        candidates.AddRange(KnownCdbPaths);
        AddIfExists(candidates, FindNearCompiler(toolchain.CompilerPath, "cdb.exe"));
        AddIfExists(candidates, FindInPath("cdb.exe"));
        return candidates;
    }

    private static IReadOnlyList<string> ResolveGdbCandidates(ResolvedToolchainContext toolchain)
    {
        var candidates = new List<string>();
        AddIfExists(candidates, FindNearCompiler(toolchain.CompilerPath, "gdb.exe"));
        AddIfExists(candidates, FindUnderToolchainRoot(toolchain.ToolchainRootPath, "gdb.exe"));
        AddIfExists(candidates, FindInPath("gdb.exe"));
        return candidates;
    }

    private static IReadOnlyList<string> ResolveLldbCandidates(ResolvedToolchainContext toolchain)
    {
        var candidates = new List<string>();
        AddIfExists(candidates, FindNearCompiler(toolchain.CompilerPath, "lldb.exe"));
        AddIfExists(candidates, FindUnderToolchainRoot(toolchain.ToolchainRootPath, "lldb.exe"));
        AddIfExists(candidates, FindInPath("lldb.exe"));
        return candidates;
    }

    private static string ResolveCustomDebuggerPath(ToolchainSettingsConfig settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DebuggerPath))
        {
            return NormalizePath(settings.DebuggerPath);
        }

        if (!string.IsNullOrWhiteSpace(settings.GdbPath))
        {
            return NormalizePath(settings.GdbPath);
        }

        return string.Empty;
    }

    private static bool TryClassifyDebugger(string debuggerPath, out DebuggerKind kind)
    {
        var fileName = Path.GetFileName(debuggerPath);
        if (fileName.Equals("cdb.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = DebuggerKind.Cdb;
            return true;
        }

        if (fileName.Equals("gdb.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = DebuggerKind.Gdb;
            return true;
        }

        if (fileName.Equals("lldb.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = DebuggerKind.Lldb;
            return true;
        }

        kind = default;
        return false;
    }

    private static string BuildNotFoundDetail(DebuggerKind requiredKind)
    {
        return $"未自动找到 {requiredKind}，请在 设置 -> 编译 中填写同类型调试器路径。";
    }

    private static string FindNearCompiler(string compilerPath, string debuggerName)
    {
        if (string.IsNullOrWhiteSpace(compilerPath))
        {
            return string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(compilerPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            var candidate = Path.Combine(directory, debuggerName);
            return File.Exists(candidate) ? candidate : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindUnderToolchainRoot(string rootPath, string debuggerName)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return string.Empty;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(rootPath);
            if (!Directory.Exists(normalizedRoot))
            {
                return string.Empty;
            }

            var direct = Path.Combine(normalizedRoot, debuggerName);
            if (File.Exists(direct))
            {
                return direct;
            }

            var binCandidate = Path.Combine(normalizedRoot, "bin", debuggerName);
            if (File.Exists(binCandidate))
            {
                return binCandidate;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string FindInPath(string executableName)
    {
        return ToolchainProbeUtilities.TryFindExecutableInPath(executableName, out var found)
            ? found
            : string.Empty;
    }

    private static void AddIfExists(ICollection<string> target, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            target.Add(path);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return path.Trim().Trim('"');
        }
    }
}

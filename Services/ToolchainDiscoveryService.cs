namespace C__Editor;

internal interface IToolchainProbe
{
    IEnumerable<ToolchainProbeHit> Probe();
}

internal sealed class ToolchainDiscoveryService
{
    private readonly IReadOnlyList<IToolchainProbe> probes;

    internal ToolchainDiscoveryService()
        : this(new IToolchainProbe[]
        {
            new BuiltInToolchainProbe(),
            new PathToolchainProbe(),
            new CommonLocationToolchainProbe()
        })
    {
    }

    internal ToolchainDiscoveryService(IReadOnlyList<IToolchainProbe> probes)
    {
        this.probes = probes;
    }

    internal IReadOnlyList<ToolchainProbeResult> Discover()
    {
        var hitById = new Dictionary<ToolchainId, ToolchainProbeHit>();
        foreach (var probe in probes)
        {
            foreach (var hit in probe.Probe())
            {
                if (!hitById.ContainsKey(hit.Id))
                {
                    hitById[hit.Id] = hit;
                }
            }
        }

        var results = new List<ToolchainProbeResult>();
        foreach (var item in ToolchainCatalog.GetItems())
        {
            if (hitById.TryGetValue(item.Id, out var hit))
            {
                results.Add(new ToolchainProbeResult
                {
                    Id = item.Id,
                    Family = item.Family,
                    DisplayName = item.DisplayName,
                    IsAvailable = true,
                    CompilerPath = hit.CompilerPath,
                    SetupScriptPath = hit.SetupScriptPath,
                    ToolchainRootPath = hit.ToolchainRootPath,
                    Source = hit.Source,
                    Detail = hit.Detail
                });
                continue;
            }

            results.Add(new ToolchainProbeResult
            {
                Id = item.Id,
                Family = item.Family,
                DisplayName = item.DisplayName,
                IsAvailable = false,
                UnavailableReason = BuildUnavailableReason(item.Id)
            });
        }

        return results;
    }

    private static string BuildUnavailableReason(ToolchainId id)
    {
        return id switch
        {
            ToolchainId.BuiltInMsvc => $"未检测到内置 MSVC: {ToolchainProbeUtilities.GetBuiltInMsvcRoot()}",
            ToolchainId.BuiltInMinGw => $"未检测到内置 MinGW: {ToolchainProbeUtilities.GetBuiltInMinGwRoot()}",
            ToolchainId.LocalMsvc => "未在 PATH 或常见安装目录中找到 cl.exe",
            ToolchainId.Gcc => "未在 PATH 或常见安装目录中找到 gcc.exe",
            ToolchainId.Gpp => "未在 PATH 或常见安装目录中找到 g++.exe",
            ToolchainId.Clang => "未在 PATH 或常见安装目录中找到 clang.exe",
            ToolchainId.ClangPlusPlus => "未在 PATH 或常见安装目录中找到 clang++.exe",
            _ => "未找到可用工具链"
        };
    }
}

internal sealed class BuiltInToolchainProbe : IToolchainProbe
{
    public IEnumerable<ToolchainProbeHit> Probe()
    {
        var builtInMsvcRoot = ToolchainProbeUtilities.GetBuiltInMsvcRoot();
        if (ToolchainProbeUtilities.TryResolveMsvcCompilerFromRoot(builtInMsvcRoot, out var msvcCompiler))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.BuiltInMsvc,
                CompilerPath = msvcCompiler,
                SetupScriptPath = ToolchainProbeUtilities.ResolveMsvcSetupScriptPath(builtInMsvcRoot),
                ToolchainRootPath = builtInMsvcRoot,
                Source = "内置目录",
                Detail = $"来自内置 MSVC: {builtInMsvcRoot}"
            };
        }

        var builtInMinGwRoot = ToolchainProbeUtilities.GetBuiltInMinGwRoot();
        var gppPath = Path.Combine(builtInMinGwRoot, "bin", "g++.exe");
        if (File.Exists(gppPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.BuiltInMinGw,
                CompilerPath = gppPath,
                ToolchainRootPath = builtInMinGwRoot,
                Source = "内置目录",
                Detail = $"来自内置 MinGW: {builtInMinGwRoot}"
            };
        }
    }
}

internal sealed class PathToolchainProbe : IToolchainProbe
{
    public IEnumerable<ToolchainProbeHit> Probe()
    {
        if (ToolchainProbeUtilities.TryFindExecutableInPath("cl.exe", out var clPath))
        {
            ToolchainProbeUtilities.TryInferMsvcRootFromCompilerPath(clPath, out var msvcRoot);
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.LocalMsvc,
                CompilerPath = clPath,
                SetupScriptPath = ToolchainProbeUtilities.ResolveMsvcSetupScriptPath(msvcRoot),
                ToolchainRootPath = msvcRoot,
                Source = "PATH",
                Detail = $"在 PATH 中找到 cl.exe: {clPath}"
            };
        }

        if (ToolchainProbeUtilities.TryFindExecutableInPath("gcc.exe", out var gccPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Gcc,
                CompilerPath = gccPath,
                ToolchainRootPath = Path.GetDirectoryName(gccPath) ?? string.Empty,
                Source = "PATH",
                Detail = $"在 PATH 中找到 gcc.exe: {gccPath}"
            };
        }

        if (ToolchainProbeUtilities.TryFindExecutableInPath("g++.exe", out var gppPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Gpp,
                CompilerPath = gppPath,
                ToolchainRootPath = Path.GetDirectoryName(gppPath) ?? string.Empty,
                Source = "PATH",
                Detail = $"在 PATH 中找到 g++.exe: {gppPath}"
            };
        }

        if (ToolchainProbeUtilities.TryFindExecutableInPath("clang.exe", out var clangPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Clang,
                CompilerPath = clangPath,
                ToolchainRootPath = Path.GetDirectoryName(clangPath) ?? string.Empty,
                Source = "PATH",
                Detail = $"在 PATH 中找到 clang.exe: {clangPath}"
            };
        }

        if (ToolchainProbeUtilities.TryFindExecutableInPath("clang++.exe", out var clangxxPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.ClangPlusPlus,
                CompilerPath = clangxxPath,
                ToolchainRootPath = Path.GetDirectoryName(clangxxPath) ?? string.Empty,
                Source = "PATH",
                Detail = $"在 PATH 中找到 clang++.exe: {clangxxPath}"
            };
        }
    }
}

internal sealed class CommonLocationToolchainProbe : IToolchainProbe
{
    public IEnumerable<ToolchainProbeHit> Probe()
    {
        if (TryProbeMsvcFromCommonLocations(out var msvcHit))
        {
            yield return msvcHit;
        }

        foreach (var hit in ProbeGnuToolchainsFromCommonLocations())
        {
            yield return hit;
        }

        foreach (var hit in ProbeClangFromCommonLocations())
        {
            yield return hit;
        }
    }

    private static bool TryProbeMsvcFromCommonLocations(out ToolchainProbeHit hit)
    {
        hit = new ToolchainProbeHit();
        var candidates = new List<(string compilerPath, string rootPath, Version version)>();
        foreach (var installRoot in ToolchainProbeUtilities.EnumerateVisualStudioInstallRoots())
        {
            if (!ToolchainProbeUtilities.TryResolveMsvcCompilerFromRoot(installRoot, out var compilerPath))
            {
                continue;
            }

            var version = ToolchainProbeUtilities.TryParseMsvcVersionFromCompilerPath(compilerPath, out var parsedVersion)
                ? parsedVersion
                : new Version(0, 0);

            candidates.Add((compilerPath, installRoot, version));
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var selected = candidates
            .OrderByDescending(item => item.version.Major)
            .ThenByDescending(item => item.version.Minor)
            .ThenByDescending(item => Math.Max(item.version.Build, 0))
            .ThenByDescending(item => Math.Max(item.version.Revision, 0))
            .First();

        hit = new ToolchainProbeHit
        {
            Id = ToolchainId.LocalMsvc,
            CompilerPath = selected.compilerPath,
            SetupScriptPath = ToolchainProbeUtilities.ResolveMsvcSetupScriptPath(selected.rootPath),
            ToolchainRootPath = selected.rootPath,
            Source = "常见目录",
            Detail = $"在常见目录找到 MSVC: {selected.compilerPath}"
        };

        return true;
    }

    private static IEnumerable<ToolchainProbeHit> ProbeGnuToolchainsFromCommonLocations()
    {
        var minGwBinRoots = ToolchainProbeUtilities.GetKnownMinGwBinPaths();
        var gccPath = ToolchainProbeUtilities.FindFirstExecutableInDirectories(minGwBinRoots, "gcc.exe");
        if (!string.IsNullOrWhiteSpace(gccPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Gcc,
                CompilerPath = gccPath,
                ToolchainRootPath = Path.GetDirectoryName(gccPath) ?? string.Empty,
                Source = "常见目录",
                Detail = $"在常见目录找到 gcc.exe: {gccPath}"
            };
        }

        var gppPath = ToolchainProbeUtilities.FindFirstExecutableInDirectories(minGwBinRoots, "g++.exe");
        if (!string.IsNullOrWhiteSpace(gppPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Gpp,
                CompilerPath = gppPath,
                ToolchainRootPath = Path.GetDirectoryName(gppPath) ?? string.Empty,
                Source = "常见目录",
                Detail = $"在常见目录找到 g++.exe: {gppPath}"
            };
        }
    }

    private static IEnumerable<ToolchainProbeHit> ProbeClangFromCommonLocations()
    {
        var clangBinRoots = ToolchainProbeUtilities.GetKnownClangBinPaths();
        var clangPath = ToolchainProbeUtilities.FindFirstExecutableInDirectories(clangBinRoots, "clang.exe");
        if (!string.IsNullOrWhiteSpace(clangPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.Clang,
                CompilerPath = clangPath,
                ToolchainRootPath = Path.GetDirectoryName(clangPath) ?? string.Empty,
                Source = "常见目录",
                Detail = $"在常见目录找到 clang.exe: {clangPath}"
            };
        }

        var clangxxPath = ToolchainProbeUtilities.FindFirstExecutableInDirectories(clangBinRoots, "clang++.exe");
        if (!string.IsNullOrWhiteSpace(clangxxPath))
        {
            yield return new ToolchainProbeHit
            {
                Id = ToolchainId.ClangPlusPlus,
                CompilerPath = clangxxPath,
                ToolchainRootPath = Path.GetDirectoryName(clangxxPath) ?? string.Empty,
                Source = "常见目录",
                Detail = $"在常见目录找到 clang++.exe: {clangxxPath}"
            };
        }
    }
}

internal static class ToolchainProbeUtilities
{
    internal static string GetBuiltInMsvcRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "msvc");
    }

    internal static string GetBuiltInMinGwRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "mingw");
    }

    internal static bool TryFindExecutableInPath(string executableName, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return false;
        }

        var rawPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        foreach (var segment in rawPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidateDirectory = segment.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(candidateDirectory))
            {
                continue;
            }

            string candidatePath;
            try
            {
                candidatePath = Path.Combine(candidateDirectory, executableName);
            }
            catch
            {
                continue;
            }

            if (File.Exists(candidatePath))
            {
                fullPath = Path.GetFullPath(candidatePath);
                return true;
            }
        }

        return false;
    }

    internal static bool TryResolveMsvcCompilerFromRoot(string toolchainRootPath, out string compilerPath)
    {
        compilerPath = string.Empty;
        if (string.IsNullOrWhiteSpace(toolchainRootPath))
        {
            return false;
        }

        var msvcToolsRoot = Path.Combine(toolchainRootPath, "VC", "Tools", "MSVC");
        if (!Directory.Exists(msvcToolsRoot))
        {
            return false;
        }

        var candidates = new List<(string path, Version version)>();
        foreach (var versionDirectory in SafeEnumerateDirectories(msvcToolsRoot))
        {
            var candidatePath = Path.Combine(versionDirectory, "bin", "Hostx64", "x64", "cl.exe");
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            var version = Version.TryParse(Path.GetFileName(versionDirectory), out var parsedVersion)
                ? parsedVersion
                : new Version(0, 0);
            candidates.Add((candidatePath, version));
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        compilerPath = candidates
            .OrderByDescending(item => item.version.Major)
            .ThenByDescending(item => item.version.Minor)
            .ThenByDescending(item => Math.Max(item.version.Build, 0))
            .ThenByDescending(item => Math.Max(item.version.Revision, 0))
            .Select(item => item.path)
            .First();

        return true;
    }

    internal static bool TryInferMsvcRootFromCompilerPath(string compilerPath, out string rootPath)
    {
        rootPath = string.Empty;
        if (string.IsNullOrWhiteSpace(compilerPath))
        {
            return false;
        }

        DirectoryInfo? current;
        try
        {
            current = new FileInfo(compilerPath).Directory;
        }
        catch
        {
            return false;
        }

        while (current is not null)
        {
            if (string.Equals(current.Name, "VC", StringComparison.OrdinalIgnoreCase) &&
                current.Parent is not null)
            {
                rootPath = current.Parent.FullName;
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    internal static string ResolveMsvcSetupScriptPath(string? msvcRootPath)
    {
        if (string.IsNullOrWhiteSpace(msvcRootPath))
        {
            return string.Empty;
        }

        var candidatePath = Path.Combine(msvcRootPath, "VC", "Auxiliary", "Build", "vcvars64.bat");
        return File.Exists(candidatePath) ? candidatePath : string.Empty;
    }

    internal static bool TryParseMsvcVersionFromCompilerPath(string compilerPath, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(compilerPath))
        {
            return false;
        }

        var marker = $"{Path.DirectorySeparatorChar}MSVC{Path.DirectorySeparatorChar}";
        var alternateMarker = $"{Path.AltDirectorySeparatorChar}MSVC{Path.AltDirectorySeparatorChar}";
        var index = compilerPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var markerLength = marker.Length;

        if (index < 0)
        {
            index = compilerPath.IndexOf(alternateMarker, StringComparison.OrdinalIgnoreCase);
            markerLength = alternateMarker.Length;
        }

        if (index < 0)
        {
            return false;
        }

        var suffix = compilerPath[(index + markerLength)..];
        var parts = suffix.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!Version.TryParse(parts[0], out var parsedVersion))
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }

    internal static IEnumerable<string> EnumerateVisualStudioInstallRoots()
    {
        var baseRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfNotEmpty(baseRoots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfNotEmpty(baseRoots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        foreach (var baseRoot in baseRoots)
        {
            var visualStudioRoot = Path.Combine(baseRoot, "Microsoft Visual Studio");
            if (!Directory.Exists(visualStudioRoot))
            {
                continue;
            }

            foreach (var yearDirectory in SafeEnumerateDirectories(visualStudioRoot))
            {
                foreach (var editionDirectory in SafeEnumerateDirectories(yearDirectory))
                {
                    yield return editionDirectory;
                }
            }
        }
    }

    internal static IReadOnlyList<string> GetKnownMinGwBinPaths()
    {
        return new[]
        {
            @"C:\msys64\ucrt64\bin",
            @"C:\msys64\mingw64\bin",
            @"C:\mingw64\bin",
            @"C:\MinGW\bin"
        };
    }

    internal static IReadOnlyList<string> GetKnownClangBinPaths()
    {
        return new[]
        {
            @"C:\Program Files\LLVM\bin"
        };
    }

    internal static string FindFirstExecutableInDirectories(IEnumerable<string> directories, string executableName)
    {
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void AddIfNotEmpty(HashSet<string> target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }
}

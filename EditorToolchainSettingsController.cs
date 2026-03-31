namespace C__Editor;

internal static class EditorToolchainSettingsController
{
    private const string BuiltInVsRoot = @"C:\Program Files\Microsoft Visual Studio\18\Community";
    private const string BuiltInMsvcVersion = "14.50.35717";

    private static readonly string BuiltInCompilerPath = Path.Combine(
        BuiltInVsRoot,
        "VC",
        "Tools",
        "MSVC",
        BuiltInMsvcVersion,
        "bin",
        "Hostx64",
        "x64",
        "cl.exe");

    private static readonly string BuiltInSetupScriptPath = Path.Combine(
        BuiltInVsRoot,
        "VC",
        "Auxiliary",
        "Build",
        "vcvars64.bat");

    internal static ToolchainSettingsConfig Get()
    {
        var settings = Normalize(EditorConfigurationController.GetToolchainSettings());
        EditorConfigurationController.SaveToolchainSettings(settings);
        return settings;
    }

    internal static void Save(ToolchainSettingsConfig settings)
    {
        EditorConfigurationController.SaveToolchainSettings(Normalize(settings));
    }

    internal static bool TryResolveCompilerExecutable(ToolchainSettingsConfig settings, out string compilerPath, out string detail)
    {
        var normalized = Normalize(settings);
        compilerPath = normalized.CompilerPath;
        detail = string.Empty;

        if (string.IsNullOrWhiteSpace(compilerPath))
        {
            detail = "未配置 MSVC 编译器路径。";
            return false;
        }

        if (!File.Exists(compilerPath))
        {
            detail = $"内置 MSVC 编译器不存在: {compilerPath}";
            return false;
        }

        if (!compilerPath.EndsWith("cl.exe", StringComparison.OrdinalIgnoreCase))
        {
            detail = $"编译器必须是 cl.exe: {compilerPath}";
            return false;
        }

        detail = $"来自内置 MSVC: {compilerPath}";
        return true;
    }

    internal static bool TryResolveCompilerSetupScript(ToolchainSettingsConfig settings, out string setupScriptPath, out string detail)
    {
        var normalized = Normalize(settings);
        setupScriptPath = normalized.SetupScriptPath;
        detail = string.Empty;

        if (string.IsNullOrWhiteSpace(setupScriptPath))
        {
            detail = "未配置 vcvars64.bat 路径。";
            return false;
        }

        if (!File.Exists(setupScriptPath))
        {
            detail = $"内置 vcvars64.bat 不存在: {setupScriptPath}";
            return false;
        }

        detail = $"使用环境脚本: {setupScriptPath}";
        return true;
    }

    internal static bool TryResolveDebuggerExecutable(ToolchainSettingsConfig settings, out string debuggerPath, out string detail)
    {
        debuggerPath = string.Empty;
        detail = "MSVC 调试器接入尚未实现。";
        return false;
    }

    private static ToolchainSettingsConfig Normalize(ToolchainSettingsConfig? settings)
    {
        var input = settings ?? ToolchainSettingsConfig.CreateDefault();
        return new ToolchainSettingsConfig
        {
            CompilerPath = string.IsNullOrWhiteSpace(input.CompilerPath)
                ? BuiltInCompilerPath
                : input.CompilerPath.Trim(),
            SetupScriptPath = string.IsNullOrWhiteSpace(input.SetupScriptPath)
                ? BuiltInSetupScriptPath
                : input.SetupScriptPath.Trim(),
            ToolchainRootPath = string.IsNullOrWhiteSpace(input.ToolchainRootPath)
                ? BuiltInVsRoot
                : input.ToolchainRootPath.Trim(),
            CompilerArguments = string.IsNullOrWhiteSpace(input.CompilerArguments)
                ? "/std:c++17 /EHsc /Zi /nologo"
                : input.CompilerArguments.Trim(),
            BuildOutputDirectory = string.IsNullOrWhiteSpace(input.BuildOutputDirectory)
                ? @".cppeditor\build"
                : input.BuildOutputDirectory.Trim(),

            // legacy MinGW fields are intentionally cleared.
            CompilerArchivePath = string.Empty,
            GppPath = string.Empty,
            GdbPath = string.Empty
        };
    }
}

namespace C__Editor;

internal static class EditorToolchainSettingsController
{
    private const string BuiltInMsvcVersion = "14.50.35717";

    private static string BuiltInRootPath => Path.Combine(AppContext.BaseDirectory, "msvc");

    private static string BuiltInCompilerPath => Path.Combine(
        BuiltInRootPath,
        "VC",
        "Tools",
        "MSVC",
        BuiltInMsvcVersion,
        "bin",
        "Hostx64",
        "x64",
        "cl.exe");

    private static string BuiltInSetupScriptPath => Path.Combine(
        BuiltInRootPath,
        "VC",
        "Auxiliary",
        "Build",
        "vcvars64.bat");

    internal static ToolchainSettingsConfig Get()
    {
        var normalized = Normalize(EditorConfigurationController.GetToolchainSettings());
        EditorConfigurationController.SaveToolchainSettings(normalized);
        return normalized;
    }

    internal static void Save(ToolchainSettingsConfig settings)
    {
        EditorConfigurationController.SaveToolchainSettings(Normalize(settings));
    }

    internal static bool TryResolveCompilerExecutable(ToolchainSettingsConfig settings, out string compilerPath, out string detail)
    {
        var normalized = Normalize(settings);
        compilerPath = normalized.CompilerPath;
        if (File.Exists(compilerPath))
        {
            detail = $"来自内置 MSVC: {compilerPath}";
            return true;
        }

        detail = $"内置 MSVC 编译器不存在: {compilerPath}";
        return false;
    }

    internal static bool TryResolveCompilerSetupScript(ToolchainSettingsConfig settings, out string setupScriptPath, out string detail)
    {
        var normalized = Normalize(settings);
        setupScriptPath = normalized.SetupScriptPath;
        if (File.Exists(setupScriptPath))
        {
            detail = $"使用内置环境脚本: {setupScriptPath}";
            return true;
        }

        detail = $"内置 vcvars64.bat 不存在: {setupScriptPath}";
        return false;
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
            CompilerPath = BuiltInCompilerPath,
            SetupScriptPath = BuiltInSetupScriptPath,
            ToolchainRootPath = BuiltInRootPath,
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

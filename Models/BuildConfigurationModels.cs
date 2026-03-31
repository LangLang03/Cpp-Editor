namespace C__Editor;

internal enum BuildConfiguration
{
    Debug,
    Release,
    Custom
}

internal sealed class BuildConfigurationSettings
{
    public BuildConfiguration Configuration { get; set; } = BuildConfiguration.Debug;

    public string CustomConfigurationName { get; set; } = string.Empty;

    // Per-toolchain arguments for Debug configuration
    public Dictionary<string, string> DebugArgumentsByToolchain { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Per-toolchain arguments for Release configuration
    public Dictionary<string, string> ReleaseArgumentsByToolchain { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> CustomConfigurations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal static BuildConfigurationSettings CreateDefault()
    {
        return new BuildConfigurationSettings
        {
            Configuration = BuildConfiguration.Debug,
            CustomConfigurationName = string.Empty,
            DebugArgumentsByToolchain = CreateDefaultArgumentsMap(BuildConfiguration.Debug),
            ReleaseArgumentsByToolchain = CreateDefaultArgumentsMap(BuildConfiguration.Release),
            CustomConfigurations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static Dictionary<string, string> CreateDefaultArgumentsMap(BuildConfiguration config)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ToolchainCatalog.GetItems())
        {
            var key = ToolchainCatalog.ToConfigValue(item.Id);
            map[key] = config == BuildConfiguration.Debug
                ? GetDefaultDebugArguments(item.Id)
                : GetDefaultReleaseArguments(item.Id);
        }

        return map;
    }

    internal BuildConfigurationSettings Clone()
    {
        return new BuildConfigurationSettings
        {
            Configuration = Configuration,
            CustomConfigurationName = CustomConfigurationName,
            DebugArgumentsByToolchain = new Dictionary<string, string>(
                DebugArgumentsByToolchain ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            ReleaseArgumentsByToolchain = new Dictionary<string, string>(
                ReleaseArgumentsByToolchain ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            CustomConfigurations = new Dictionary<string, string>(
                CustomConfigurations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    internal string GetArgumentsForCurrentConfig(ToolchainId toolchainId)
    {
        var toolchainKey = ToolchainCatalog.ToConfigValue(toolchainId);
        var defaultArgs = ToolchainCatalog.GetDefaultArguments(toolchainId);

        return Configuration switch
        {
            BuildConfiguration.Debug => GetDebugArguments(toolchainKey, toolchainId),
            BuildConfiguration.Release => GetReleaseArguments(toolchainKey, toolchainId),
            BuildConfiguration.Custom => GetCustomArguments(defaultArgs),
            _ => defaultArgs
        };
    }

    private string GetDebugArguments(string toolchainKey, ToolchainId toolchainId)
    {
        if (DebugArgumentsByToolchain.TryGetValue(toolchainKey, out var args) && !string.IsNullOrWhiteSpace(args))
        {
            return args;
        }
        return GetDefaultDebugArguments(toolchainId);
    }

    private string GetReleaseArguments(string toolchainKey, ToolchainId toolchainId)
    {
        if (ReleaseArgumentsByToolchain.TryGetValue(toolchainKey, out var args) && !string.IsNullOrWhiteSpace(args))
        {
            return args;
        }
        return GetDefaultReleaseArguments(toolchainId);
    }

    internal void SetArgumentsForConfig(BuildConfiguration config, ToolchainId toolchainId, string arguments)
    {
        var toolchainKey = ToolchainCatalog.ToConfigValue(toolchainId);

        switch (config)
        {
            case BuildConfiguration.Debug:
                DebugArgumentsByToolchain[toolchainKey] = arguments;
                break;
            case BuildConfiguration.Release:
                ReleaseArgumentsByToolchain[toolchainKey] = arguments;
                break;
            case BuildConfiguration.Custom:
                if (!string.IsNullOrWhiteSpace(CustomConfigurationName))
                {
                    CustomConfigurations[CustomConfigurationName] = arguments;
                }
                break;
        }
    }

    private static string GetDefaultDebugArguments(ToolchainId toolchainId)
    {
        return ToolchainCatalog.GetItem(toolchainId).Family == ToolchainFamily.Msvc
            ? "/std:c++17 /EHsc /Zi /nologo /Od /MDd"
            : "-std=c++17 -g -O0 -Wall";
    }

    private static string GetDefaultReleaseArguments(ToolchainId toolchainId)
    {
        return ToolchainCatalog.GetItem(toolchainId).Family == ToolchainFamily.Msvc
            ? "/std:c++17 /EHsc /nologo /O2 /MD"
            : "-std=c++17 -O2 -Wall -DNDEBUG";
    }

    private string GetCustomArguments(string defaultArgs)
    {
        if (string.IsNullOrWhiteSpace(CustomConfigurationName))
        {
            return defaultArgs;
        }

        return CustomConfigurations.TryGetValue(CustomConfigurationName, out var args) && !string.IsNullOrWhiteSpace(args)
            ? args
            : defaultArgs;
    }
}

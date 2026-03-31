namespace C__Editor;

internal static class EditorToolchainSettingsController
{
    private static readonly ToolchainDiscoveryService DiscoveryService = new();
    private static readonly ToolchainResolver Resolver = new(DiscoveryService);

    internal static ToolchainSettingsConfig Get()
    {
        var normalized = EditorConfigurationController.GetToolchainSettings();
        EditorConfigurationController.SaveToolchainSettings(normalized);
        return normalized;
    }

    internal static void Save(ToolchainSettingsConfig settings)
    {
        EditorConfigurationController.SaveToolchainSettings(settings ?? ToolchainSettingsConfig.CreateDefault());
    }

    internal static IReadOnlyList<ToolchainProbeResult> DiscoverToolchains()
    {
        return DiscoveryService.Discover();
    }

    internal static ToolchainId GetSelectedToolchainId(ToolchainSettingsConfig? settings)
    {
        return Resolver.GetSelectedToolchainId(settings);
    }

    internal static Dictionary<ToolchainId, string> GetArgumentsByToolchain(ToolchainSettingsConfig? settings)
    {
        return Resolver.GetArgumentsByToolchain(settings);
    }

    internal static string GetArgumentsForToolchain(ToolchainSettingsConfig? settings, ToolchainId id)
    {
        var map = Resolver.GetArgumentsByToolchain(settings);
        return map.TryGetValue(id, out var value)
            ? value
            : ToolchainCatalog.GetDefaultArguments(id);
    }

    internal static bool TryResolveSelectedToolchain(
        ToolchainSettingsConfig? settings,
        out ResolvedToolchainContext context,
        out string detail)
    {
        return Resolver.TryResolveSelected(settings, out context, out detail);
    }

    internal static bool TryResolveDebuggerExecutable(ToolchainSettingsConfig settings, out string debuggerPath, out string detail)
    {
        debuggerPath = string.Empty;
        detail = "调试器接入尚未实现。";
        return false;
    }
}

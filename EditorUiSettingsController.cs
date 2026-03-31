namespace C__Editor;

internal static class EditorUiSettingsController
{
    internal static UiSettings Get()
    {
        return EditorConfigurationController.GetUiSettings();
    }

    internal static void Save(UiSettings settings)
    {
        EditorConfigurationController.SaveUiSettings(settings);
    }
}

internal sealed class UiSettings
{
    public bool ShowProjectTree { get; set; } = true;

    public bool ShowOutputPanel { get; set; } = true;

    public int ExplorerWidth { get; set; } = 220;

    internal UiSettings Clone()
    {
        return new UiSettings
        {
            ShowProjectTree = ShowProjectTree,
            ShowOutputPanel = ShowOutputPanel,
            ExplorerWidth = ExplorerWidth
        };
    }
}

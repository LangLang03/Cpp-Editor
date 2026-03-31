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

    public bool RestoreLastSessionOnStartup { get; set; } = true;

    public string ThemeId { get; set; } = EditorThemeController.LightThemeId;

    internal UiSettings Clone()
    {
        return new UiSettings
        {
            ShowProjectTree = ShowProjectTree,
            ShowOutputPanel = ShowOutputPanel,
            ExplorerWidth = ExplorerWidth,
            RestoreLastSessionOnStartup = RestoreLastSessionOnStartup,
            ThemeId = ThemeId
        };
    }
}

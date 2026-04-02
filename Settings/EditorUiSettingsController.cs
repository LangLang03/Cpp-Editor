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
    public const int ExplorerWidthMin = 180;
    public const int ExplorerWidthMax = 420;
    public const int ExplorerWidthDefault = 220;
    public const int OutputPanelHeightMin = 140;
    public const int OutputPanelHeightMax = 520;
    public const int OutputPanelHeightDefault = 280;
    public const int CodeStructurePanelWidthMin = 200;
    public const int CodeStructurePanelWidthMax = 400;
    public const int CodeStructurePanelWidthDefault = 280;

    public bool ShowProjectTree { get; set; } = true;

    public bool ShowOutputPanel { get; set; } = true;

    public int ExplorerWidth { get; set; } = ExplorerWidthDefault;

    public int OutputPanelHeight { get; set; } = OutputPanelHeightDefault;

    public int CodeStructurePanelWidth { get; set; } = CodeStructurePanelWidthDefault;

    public bool RestoreLastSessionOnStartup { get; set; } = true;

    public string ThemeId { get; set; } = EditorThemeController.LightThemeId;

    internal UiSettings Clone()
    {
        return new UiSettings
        {
            ShowProjectTree = ShowProjectTree,
            ShowOutputPanel = ShowOutputPanel,
            ExplorerWidth = ExplorerWidth,
            OutputPanelHeight = OutputPanelHeight,
            CodeStructurePanelWidth = CodeStructurePanelWidth,
            RestoreLastSessionOnStartup = RestoreLastSessionOnStartup,
            ThemeId = ThemeId
        };
    }
}

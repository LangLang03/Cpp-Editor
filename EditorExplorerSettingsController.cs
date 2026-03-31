namespace C__Editor;

internal static class EditorExplorerSettingsController
{
    internal static ExplorerSettingsConfig Get()
    {
        return EditorConfigurationController.GetExplorerSettings();
    }

    internal static void Save(ExplorerSettingsConfig settings)
    {
        EditorConfigurationController.SaveExplorerSettings(settings);
    }
}

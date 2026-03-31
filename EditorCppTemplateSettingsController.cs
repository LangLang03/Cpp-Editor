namespace C__Editor;

internal static class EditorCppTemplateSettingsController
{
    internal static CppTemplateSettingsConfig Get()
    {
        return EditorConfigurationController.GetCppTemplateSettings();
    }

    internal static void Save(CppTemplateSettingsConfig settings)
    {
        EditorConfigurationController.SaveCppTemplateSettings(settings);
    }
}

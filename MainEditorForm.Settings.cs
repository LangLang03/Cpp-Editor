namespace C__Editor;

public partial class MainEditorForm
{
    private UiSettings uiSettings = new();
    private ToolchainSettingsConfig toolchainSettings = ToolchainSettingsConfig.CreateDefault();
    private bool suppressViewMenuStateSync;

    private void InitializeUserSettings()
    {
        uiSettings = EditorUiSettingsController.Get();
        toolchainSettings = EditorToolchainSettingsController.Get();
        ReloadShortcutBindings();
        ApplyUiSettings(uiSettings);
        UpdateBuildRunMenuState();
    }

    private void ApplyUiSettings(UiSettings settings)
    {
        uiSettings = settings.Clone();

        splitWorkspace.Panel1Collapsed = !uiSettings.ShowProjectTree;
        splitMain.Panel2Collapsed = !uiSettings.ShowOutputPanel;

        if (!splitWorkspace.Panel1Collapsed && splitWorkspace.Width > 0)
        {
            splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        }

        SyncViewMenuState();
    }

    private void SyncViewMenuState()
    {
        if (menuViewProjectTree is null || menuViewOutputWindow is null)
        {
            return;
        }

        suppressViewMenuStateSync = true;
        try
        {
            menuViewProjectTree.Checked = !splitWorkspace.Panel1Collapsed;
            menuViewOutputWindow.Checked = !splitMain.Panel2Collapsed;
        }
        finally
        {
            suppressViewMenuStateSync = false;
        }
    }

    private void PersistUiSettingsFromCurrentState()
    {
        uiSettings.ShowProjectTree = !splitWorkspace.Panel1Collapsed;
        uiSettings.ShowOutputPanel = !splitMain.Panel2Collapsed;
        if (!splitWorkspace.Panel1Collapsed && splitWorkspace.SplitterDistance > 0)
        {
            uiSettings.ExplorerWidth = splitWorkspace.SplitterDistance;
        }

        EditorUiSettingsController.Save(uiSettings);
    }

    private void OpenSettingsDialog()
    {
        var currentPairFormat = EditorAutoPairController.GetPairFormat();
        var currentShortcutBindings = GetShortcutBindingsForEditing();
        using var dialog = new EditorSettingsForm(currentPairFormat, uiSettings, currentShortcutBindings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        EditorAutoPairController.SetPairFormat(dialog.AutoPairFormat);
        SaveShortcutBindingsFromSettings(dialog.ResultShortcutBindings);
        ApplyUiSettings(dialog.ResultUiSettings);
        PersistUiSettingsFromCurrentState();

        ApplyEditorLanguageConfiguration(currentEditorFilePath ?? "untitled.cpp");
        AppendBuildOutput("Settings applied");
    }

    private void OpenCompilerSettingsDialog()
    {
        using var dialog = new ToolchainSettingsForm(toolchainSettings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        toolchainSettings = dialog.ResultSettings;
        EditorToolchainSettingsController.Save(toolchainSettings);
        AppendBuildOutput("编译器设置已保存。");
    }
}

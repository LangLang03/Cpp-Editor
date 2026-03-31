namespace C__Editor;

public partial class MainEditorForm
{
    private UiSettings uiSettings = new();
    private ToolchainSettingsConfig toolchainSettings = ToolchainSettingsConfig.CreateDefault();
    private ExplorerSettingsConfig explorerSettings = new();
    private CppTemplateSettingsConfig cppTemplateSettings = CppTemplateSettingsConfig.CreateDefault();
    private bool suppressViewMenuStateSync;

    private void InitializeUserSettings()
    {
        uiSettings = EditorUiSettingsController.Get();
        toolchainSettings = EditorToolchainSettingsController.Get();
        explorerSettings = EditorExplorerSettingsController.Get();
        cppTemplateSettings = EditorCppTemplateSettingsController.Get();
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
        using var dialog = new EditorSettingsForm(
            currentPairFormat,
            uiSettings,
            explorerSettings,
            cppTemplateSettings,
            currentShortcutBindings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        EditorAutoPairController.SetPairFormat(dialog.AutoPairFormat);
        explorerSettings = dialog.ResultExplorerSettings;
        cppTemplateSettings = dialog.ResultCppTemplateSettings;
        EditorExplorerSettingsController.Save(explorerSettings);
        EditorCppTemplateSettingsController.Save(cppTemplateSettings);
        SaveShortcutBindingsFromSettings(dialog.ResultShortcutBindings);
        ApplyUiSettings(dialog.ResultUiSettings);
        PersistUiSettingsFromCurrentState();

        ApplyEditorLanguageConfiguration(currentEditorFilePath ?? "untitled.cpp");
        AppendBuildOutput("Settings applied");
    }

    private void OpenCompilerSettingsDialog()
    {
        var workspaceRoot = ResolvePreferredWorkspaceRoot();
        var compileListConfig = WorkspaceCompileListController.Load(workspaceRoot);

        using var dialog = new ToolchainSettingsForm(toolchainSettings, workspaceRoot, compileListConfig.Include);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        toolchainSettings = dialog.ResultSettings;
        EditorToolchainSettingsController.Save(toolchainSettings);
        WorkspaceCompileListController.Save(workspaceRoot, dialog.ResultCompileListPatterns);
        AppendBuildOutput("编译器设置已保存。");
        AppendBuildOutput($"编译列表已保存: {WorkspaceCompileListController.GetConfigPath(workspaceRoot)}");
    }

    private string ResolvePreferredWorkspaceRoot()
    {
        var selectedState = GetSelectedDocumentState();
        if (selectedState is not null && !string.IsNullOrWhiteSpace(selectedState.FilePath))
        {
            var sourcePath = Path.GetFullPath(selectedState.FilePath);
            var sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return ResolveWorkspaceRootForSource(sourcePath, sourceDirectory);
            }
        }

        var selectedDirectory = GetTargetDirectory(treeProject?.SelectedNode);
        if (!string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return Path.GetFullPath(selectedDirectory);
        }

        if (treeProject is not null)
        {
            foreach (TreeNode rootNode in treeProject.Nodes)
            {
                var nodeData = GetNodeData(rootNode);
                if (nodeData?.Kind == ExplorerNodeKind.Directory &&
                    !string.IsNullOrWhiteSpace(nodeData.FullPath) &&
                    Directory.Exists(nodeData.FullPath))
                {
                    return Path.GetFullPath(nodeData.FullPath);
                }
            }
        }

        return Environment.CurrentDirectory;
    }
}

namespace C__Editor;

public partial class MainEditorForm : Form
{
    public MainEditorForm()
    {
        InitializeComponent();
        LoadAdditionalSettings();
        ApplySelectedTheme();
        InitializeUserSettings();
        RestoreLastSessionOnStartupIfNeeded();
        FormClosing += MainEditorForm_FormClosing;
    }

    private void LoadAdditionalSettings()
    {
        // Load build configuration settings
        buildConfigurationSettings = EditorConfigurationController.GetBuildConfigurationSettings();
        
        // Update menu check states
        if (menuBuildConfigDebug is not null)
        {
            menuBuildConfigDebug.Checked = buildConfigurationSettings.Configuration == BuildConfiguration.Debug;
        }
        if (menuBuildConfigRelease is not null)
        {
            menuBuildConfigRelease.Checked = buildConfigurationSettings.Configuration == BuildConfiguration.Release;
        }
        
        // Load code structure settings
        codeStructureSettings = EditorConfigurationController.GetCodeStructureSettings();
        codeStructureBrowser?.SetSettings(codeStructureSettings);
    }

    private void ApplySelectedTheme()
    {
        EditorThemeController.ApplyTheme(
            uiSettings.ThemeId,
            this,
            menuMain,
            splitMain,
            splitWorkspace,
            treeProject,
            tabEditorHost,
            tabBottom,
            rtbBuildOutput,
            dgvCompileErrors,
            rtbRunOutput,
            rtbRuntimeLog,
            statusEditor);

        if (editorControlMain is not null)
        {
            EditorThemeController.ApplyTheme(uiSettings.ThemeId, editorControlMain);
        }
    }

    private void MainEditorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!EnsureCanCloseAllDocuments(closeTabs: false))
        {
            e.Cancel = true;
            return;
        }

        PersistUiSettingsFromCurrentState();
        PersistSessionStateOnExit();
    }
}

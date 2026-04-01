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

        if (dgvDebugVariables is not null && dgvCompileErrors is not null)
        {
            dgvDebugVariables.BackgroundColor = dgvCompileErrors.BackgroundColor;
            dgvDebugVariables.GridColor = dgvCompileErrors.GridColor;
            dgvDebugVariables.EnableHeadersVisualStyles = dgvCompileErrors.EnableHeadersVisualStyles;
            dgvDebugVariables.ColumnHeadersDefaultCellStyle.BackColor = dgvCompileErrors.ColumnHeadersDefaultCellStyle.BackColor;
            dgvDebugVariables.ColumnHeadersDefaultCellStyle.ForeColor = dgvCompileErrors.ColumnHeadersDefaultCellStyle.ForeColor;
            dgvDebugVariables.DefaultCellStyle.BackColor = dgvCompileErrors.DefaultCellStyle.BackColor;
            dgvDebugVariables.DefaultCellStyle.ForeColor = dgvCompileErrors.DefaultCellStyle.ForeColor;
            dgvDebugVariables.DefaultCellStyle.SelectionBackColor = dgvCompileErrors.DefaultCellStyle.SelectionBackColor;
            dgvDebugVariables.DefaultCellStyle.SelectionForeColor = dgvCompileErrors.DefaultCellStyle.SelectionForeColor;
            dgvDebugVariables.AlternatingRowsDefaultCellStyle.BackColor = dgvCompileErrors.AlternatingRowsDefaultCellStyle.BackColor;
            dgvDebugVariables.AlternatingRowsDefaultCellStyle.ForeColor = dgvCompileErrors.AlternatingRowsDefaultCellStyle.ForeColor;
        }

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

        ShutdownDebuggerOnFormClosing();

        PersistUiSettingsFromCurrentState();
        PersistSessionStateOnExit();
    }
}

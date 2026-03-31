namespace C__Editor;

public partial class MainEditorForm : Form
{
    public MainEditorForm()
    {
        InitializeComponent();
        ApplyLightTheme();
        InitializeUserSettings();
        RestoreLastSessionOnStartupIfNeeded();
        FormClosing += MainEditorForm_FormClosing;
    }

    private void ApplyLightTheme()
    {
        EditorThemeController.ApplyLightTheme(
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
            rtbRuntimeLog);
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

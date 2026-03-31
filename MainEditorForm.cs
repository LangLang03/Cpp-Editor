namespace C__Editor;

public partial class MainEditorForm : Form
{
    public MainEditorForm()
    {
        InitializeComponent();
        ApplyLightTheme();
        InitializeUserSettings();
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
            rtbRunOutput);
    }

    private void MainEditorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!EnsureCanCloseAllDocuments())
        {
            e.Cancel = true;
            return;
        }

        PersistUiSettingsFromCurrentState();
    }
}

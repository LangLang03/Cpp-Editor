namespace C__Editor;

public partial class MainEditorForm : Form
{
    public MainEditorForm()
    {
        InitializeComponent();
        ApplySelectedTheme();
        InitializeUserSettings();
        RestoreLastSessionOnStartupIfNeeded();
        FormClosing += MainEditorForm_FormClosing;
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
            rtbRuntimeLog);

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

namespace C__Editor;

public partial class MainEditorForm : Form
{
    public MainEditorForm()
    {
        InitializeComponent();
        ApplyLightTheme();
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
}

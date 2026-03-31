namespace C__Editor;

public partial class MainEditorForm
{
    private const int ExplorerPanelMinWidth = 180;
    private const int ExplorerPanelMaxWidth = 420;
    private bool hasAppliedInitialExplorerWidth;

    private System.ComponentModel.IContainer? components;
    private MenuStrip menuMain = null!;
    private SplitContainer splitMain = null!;
    private SplitContainer splitWorkspace = null!;
    private TreeView treeProject = null!;
    private TabControl tabEditorHost = null!;
    private SweetEditor.EditorControl editorControlMain = null!;
    private TabControl tabBottom = null!;
    private RichTextBox rtbBuildOutput = null!;
    private DataGridView dgvCompileErrors = null!;
    private RichTextBox rtbRunOutput = null!;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        menuMain = CreateMainMenu();
        splitMain = new SplitContainer();
        splitWorkspace = new SplitContainer();
        treeProject = CreateProjectTree();
        tabEditorHost = CreateEditorTabs();
        tabBottom = CreateBottomTabs();

        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.SuspendLayout();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitWorkspace).BeginInit();
        splitWorkspace.SuspendLayout();
        splitWorkspace.Panel1.SuspendLayout();
        splitWorkspace.Panel2.SuspendLayout();
        SuspendLayout();

        // splitMain
        splitMain.Dock = DockStyle.Fill;
        splitMain.Name = "splitMain";
        splitMain.Orientation = Orientation.Horizontal;
        splitMain.TabIndex = 1;

        // splitWorkspace
        splitWorkspace.Dock = DockStyle.Fill;
        splitWorkspace.FixedPanel = FixedPanel.Panel1;
        splitWorkspace.Name = "splitWorkspace";
        splitWorkspace.Panel1MinSize = ExplorerPanelMinWidth;
        splitWorkspace.TabIndex = 0;
        splitWorkspace.SplitterMoved += SplitWorkspace_SplitterMoved;

        splitWorkspace.Panel1.Controls.Add(treeProject);
        splitWorkspace.Panel2.Controls.Add(tabEditorHost);
        splitMain.Panel1.Controls.Add(splitWorkspace);
        splitMain.Panel2.Controls.Add(tabBottom);

        // MainEditorForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 800);
        Controls.Add(splitMain);
        Controls.Add(menuMain);
        MainMenuStrip = menuMain;
        MinimumSize = new Size(1100, 700);
        Name = "MainEditorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "C++Editor";
        WindowState = FormWindowState.Maximized;
        Shown += MainEditorForm_Shown;
        splitMain.SplitterDistance = Math.Max(260, (int)(ClientSize.Height * 0.68));

        splitWorkspace.Panel1.ResumeLayout(false);
        splitWorkspace.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitWorkspace).EndInit();
        splitWorkspace.ResumeLayout(false);
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private void MainEditorForm_Shown(object? sender, EventArgs e)
    {
        if (hasAppliedInitialExplorerWidth)
        {
            return;
        }

        hasAppliedInitialExplorerWidth = true;
        if (!splitWorkspace.Panel1Collapsed)
        {
            splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, 420);
        }
    }

    private void SplitWorkspace_SplitterMoved(object? sender, SplitterEventArgs e)
    {
        if (splitWorkspace.Panel1Collapsed)
        {
            return;
        }

        uiSettings.ExplorerWidth = Math.Clamp(splitWorkspace.SplitterDistance, ExplorerPanelMinWidth, 420);
        PersistUiSettingsFromCurrentState();
    }
}

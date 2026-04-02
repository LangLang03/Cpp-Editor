namespace C__Editor;

public partial class MainEditorForm
{
    private const int ExplorerPanelMinWidth = 180;
    private const int ExplorerPanelMaxWidth = 420;
    private const int CodeStructurePanelMinWidth = 200;
    private const int CodeStructurePanelMaxWidth = 400;
    private const int OutputPanelMinHeight = 140;
    private const int OutputPanelMaxHeight = 520;
    private const int EditorPanelMinWidth = 260;
    private const int EditorPanelMinHeight = 240;
    private bool hasAppliedInitialExplorerWidth;

    private System.ComponentModel.IContainer? components;
    private MenuStrip menuMain = null!;
    private SplitContainer splitMain = null!;
    private SplitContainer splitWorkspace = null!;
    private SplitContainer splitEditor = null!;
    private TreeView treeProject = null!;
    private TabControl tabEditorHost = null!;
    private SweetEditor.EditorControl editorControlMain = null!;
    private CodeStructureBrowser codeStructureBrowser = null!;
    private TabControl tabBottom = null!;
    private StatusStrip statusEditor = null!;
    private ToolStripStatusLabel statusEditorSpacer = null!;
    private ToolStripStatusLabel statusEditorInfo = null!;
    private RichTextBox rtbBuildOutput = null!;
    private DataGridView dgvCompileErrors = null!;
    private DataGridView dgvDebugVariables = null!;
    private RichTextBox rtbRunOutput = null!;
    private RichTextBox rtbRuntimeLog = null!;

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
        splitEditor = new SplitContainer();
        treeProject = CreateProjectTree();
        tabEditorHost = CreateEditorTabs();
        codeStructureBrowser = new CodeStructureBrowser();
        tabBottom = CreateBottomTabs();
        statusEditor = CreateEditorStatusBar();

        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.SuspendLayout();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitWorkspace).BeginInit();
        splitWorkspace.SuspendLayout();
        splitWorkspace.Panel1.SuspendLayout();
        splitWorkspace.Panel2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitEditor).BeginInit();
        splitEditor.SuspendLayout();
        splitEditor.Panel1.SuspendLayout();
        splitEditor.Panel2.SuspendLayout();
        SuspendLayout();

        // splitMain - splits workspace and bottom panel
        splitMain.Dock = DockStyle.Fill;
        splitMain.Name = "splitMain";
        splitMain.Orientation = Orientation.Horizontal;
        splitMain.TabIndex = 1;
        splitMain.SplitterMoved += SplitMain_SplitterMoved;

        // splitWorkspace - splits project tree and editor area
        splitWorkspace.Dock = DockStyle.Fill;
        splitWorkspace.FixedPanel = FixedPanel.Panel1;
        splitWorkspace.Name = "splitWorkspace";
        splitWorkspace.Panel1MinSize = ExplorerPanelMinWidth;
        splitWorkspace.TabIndex = 0;
        splitWorkspace.SplitterMoved += SplitWorkspace_SplitterMoved;

        // splitEditor - splits editor tabs and code structure browser
        splitEditor.Dock = DockStyle.Fill;
        splitEditor.FixedPanel = FixedPanel.Panel2;
        splitEditor.Name = "splitEditor";
        splitEditor.Panel1MinSize = EditorPanelMinWidth;
        // Note: Panel2MinSize is set in Shown event to avoid initialization issues
        splitEditor.Panel2Collapsed = true; // Initially collapsed, will show in Shown event
        splitEditor.TabIndex = 0;
        splitEditor.SplitterMoved += SplitEditor_SplitterMoved;

        splitWorkspace.Panel1.Controls.Add(treeProject);
        splitWorkspace.Panel2.Controls.Add(splitEditor);
        
        splitEditor.Panel1.Controls.Add(tabEditorHost);
        splitEditor.Panel2.Controls.Add(codeStructureBrowser);
        
        splitMain.Panel1.Controls.Add(splitWorkspace);
        splitMain.Panel2.Controls.Add(tabBottom);

        // MainEditorForm
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1400, 900);
        Controls.Add(splitMain);
        Controls.Add(statusEditor);
        Controls.Add(menuMain);
        MainMenuStrip = menuMain;
        MinimumSize = new Size(1200, 700);
        Name = "MainEditorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "C++Editor";
        WindowState = FormWindowState.Maximized;
        Shown += MainEditorForm_Shown;
        splitMain.SplitterDistance = Math.Max(320, ClientSize.Height - UiSettings.OutputPanelHeightDefault);

        splitEditor.Panel1.ResumeLayout(false);
        splitEditor.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitEditor).EndInit();
        splitEditor.ResumeLayout(false);
        
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

        // Setup code structure browser
        codeStructureBrowser.ElementDoubleClicked += CodeStructureBrowser_ElementDoubleClicked;
        codeStructureBrowser.SettingsChanged += CodeStructureBrowser_SettingsChanged;
        codeStructureBrowser.RefreshRequested += CodeStructureBrowser_RefreshRequested;
        UpdateEditorStatusBar();
    }

    private void CodeStructureBrowser_SettingsChanged(object? sender, CodeStructureSettingsEventArgs e)
    {
        codeStructureSettings = e.Settings.Clone();
        EditorConfigurationController.SaveCodeStructureSettings(e.Settings);
    }

    private void CodeStructureBrowser_RefreshRequested(object? sender, EventArgs e)
    {
        RefreshCodeStructureBrowser();
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
            splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        }
        
        // Show and set initial code structure panel width
        splitEditor.Panel2MinSize = CodeStructurePanelMinWidth;
        splitEditor.Panel2Collapsed = false;

        // Delay setting SplitterDistance to ensure layout is complete
        BeginInvoke(new Action(() =>
        {
            if (!splitMain.Panel2Collapsed)
            {
                ApplyOutputPanelHeight(uiSettings.OutputPanelHeight);
            }

            ApplyCodeStructurePanelWidth(uiSettings.CodeStructurePanelWidth);
        }));
    }

    private void SplitMain_SplitterMoved(object? sender, SplitterEventArgs e)
    {
        if (splitMain.Panel2Collapsed)
        {
            return;
        }

        uiSettings.OutputPanelHeight = GetCurrentOutputPanelHeight();
        PersistUiSettingsFromCurrentState();
    }

    private void SplitWorkspace_SplitterMoved(object? sender, SplitterEventArgs e)
    {
        if (splitWorkspace.Panel1Collapsed)
        {
            return;
        }

        uiSettings.ExplorerWidth = Math.Clamp(splitWorkspace.SplitterDistance, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        PersistUiSettingsFromCurrentState();
    }

    private void SplitEditor_SplitterMoved(object? sender, SplitterEventArgs e)
    {
        if (splitEditor.Panel2Collapsed)
        {
            return;
        }

        uiSettings.CodeStructurePanelWidth = GetCurrentCodeStructurePanelWidth();
        PersistUiSettingsFromCurrentState();
    }

    private void ApplyOutputPanelHeight(int requestedHeight)
    {
        if (splitMain.Panel2Collapsed || splitMain.Height <= 0)
        {
            return;
        }

        var preferredOutputHeight = Math.Clamp(requestedHeight, OutputPanelMinHeight, OutputPanelMaxHeight);
        var maxSplitterDistance = Math.Max(0, splitMain.Height - OutputPanelMinHeight);
        var minSplitterDistance = Math.Min(EditorPanelMinHeight, maxSplitterDistance);
        var desiredSplitterDistance = splitMain.Height - preferredOutputHeight;
        splitMain.SplitterDistance = Math.Clamp(desiredSplitterDistance, minSplitterDistance, maxSplitterDistance);
    }

    private void ApplyCodeStructurePanelWidth(int requestedWidth)
    {
        if (splitEditor.Panel2Collapsed || splitEditor.Width <= 0)
        {
            return;
        }

        var maxAllowedWidth = Math.Max(
            CodeStructurePanelMinWidth,
            Math.Min(CodeStructurePanelMaxWidth, splitEditor.Width - splitEditor.Panel1MinSize));
        var targetWidth = Math.Clamp(requestedWidth, CodeStructurePanelMinWidth, maxAllowedWidth);
        var maxSplitterDistance = Math.Max(0, splitEditor.Width - CodeStructurePanelMinWidth);
        if (maxSplitterDistance <= 0)
        {
            return;
        }

        var minSplitterDistance = Math.Min(splitEditor.Panel1MinSize, maxSplitterDistance);
        var desiredSplitterDistance = splitEditor.Width - targetWidth;
        splitEditor.SplitterDistance = Math.Clamp(desiredSplitterDistance, minSplitterDistance, maxSplitterDistance);
    }

    private int GetCurrentOutputPanelHeight()
    {
        var outputHeight = splitMain.Height - splitMain.SplitterDistance - splitMain.SplitterWidth;
        return Math.Clamp(outputHeight, OutputPanelMinHeight, OutputPanelMaxHeight);
    }

    private int GetCurrentCodeStructurePanelWidth()
    {
        var panelWidth = splitEditor.Width - splitEditor.SplitterDistance - splitEditor.SplitterWidth;
        return Math.Clamp(panelWidth, CodeStructurePanelMinWidth, CodeStructurePanelMaxWidth);
    }

    private void CodeStructureBrowser_ElementDoubleClicked(object? sender, CodeElementEventArgs e)
    {
        // Navigate to the code element in the editor
        GoToLineInEditor(e.Element.LineNumber, e.Element.ColumnNumber);
    }
}

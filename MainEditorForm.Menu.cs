namespace C__Editor;

public partial class MainEditorForm
{
    private ToolStripMenuItem menuFileNew = null!;
    private ToolStripMenuItem menuFileOpen = null!;
    private ToolStripMenuItem menuFileOpenFolder = null!;

    private MenuStrip CreateMainMenu()
    {
        var menu = new MenuStrip
        {
            Name = "menuMain",
            ImageScalingSize = new Size(20, 20),
            TabIndex = 0
        };

        menuFileNew = CreateLeaf("menuFileNew", "\u65B0\u5EFA");
        menuFileNew.ShortcutKeys = Keys.Control | Keys.N;
        menuFileNew.Click += (_, _) => CreateGeneralFile();

        menuFileOpen = CreateLeaf("menuFileOpen", "\u6253\u5F00\u6587\u4EF6...");
        menuFileOpen.ShortcutKeys = Keys.Control | Keys.O;
        menuFileOpen.Click += (_, _) => OpenFilesFromDialog();

        menuFileOpenFolder = CreateLeaf("menuFileOpenFolder", "\u6253\u5F00\u6587\u4EF6\u5939...");
        menuFileOpenFolder.ShortcutKeys = Keys.Control | Keys.Shift | Keys.O;
        menuFileOpenFolder.Click += (_, _) => OpenFolderFromDialog();

        var menuFileSave = CreateLeaf("menuFileSave", "\u4FDD\u5B58");
        var menuFileSaveAs = CreateLeaf("menuFileSaveAs", "\u53E6\u5B58\u4E3A");
        var menuFileClose = CreateLeaf("menuFileClose", "\u5173\u95ED\u6587\u4EF6");
        var menuFileExit = CreateLeaf("menuFileExit", "\u9000\u51FA");
        menuFileExit.Click += (_, _) => Close();

        var menuFile = CreateMenu("menuFile", "\u6587\u4EF6",
            menuFileNew,
            menuFileOpen,
            menuFileOpenFolder,
            menuFileSave,
            menuFileSaveAs,
            menuFileClose,
            menuFileExit);

        var menuEdit = CreateMenu("menuEdit", "\u7F16\u8F91",
            CreateLeaf("menuEditUndo", "\u64A4\u9500"),
            CreateLeaf("menuEditRedo", "\u91CD\u505A"),
            CreateLeaf("menuEditCut", "\u526A\u5207"),
            CreateLeaf("menuEditCopy", "\u590D\u5236"),
            CreateLeaf("menuEditPaste", "\u7C98\u8D34"),
            CreateLeaf("menuEditFind", "\u67E5\u627E"),
            CreateLeaf("menuEditReplace", "\u66FF\u6362"),
            CreateLeaf("menuEditGoToLine", "\u8F6C\u5230\u884C"));

        var menuView = CreateMenu("menuView", "\u89C6\u56FE",
            CreateLeaf("menuViewProjectTree", "\u663E\u793A\u9879\u76EE\u6811"),
            CreateLeaf("menuViewOutputWindow", "\u663E\u793A\u8F93\u51FA\u7A97\u53E3"),
            CreateLeaf("menuViewResetLayout", "\u91CD\u7F6E\u5E03\u5C40"));

        var menuProject = CreateMenu("menuProject", "\u9879\u76EE",
            CreateLeaf("menuProjectNew", "\u65B0\u5EFA\u9879\u76EE"),
            CreateLeaf("menuProjectOpen", "\u6253\u5F00\u9879\u76EE"),
            CreateLeaf("menuProjectClose", "\u5173\u95ED\u9879\u76EE"),
            CreateLeaf("menuProjectProperties", "\u9879\u76EE\u5C5E\u6027"));

        var menuBuild = CreateMenu("menuBuild", "\u7F16\u8BD1",
            CreateLeaf("menuBuildCompile", "\u7F16\u8BD1"),
            CreateLeaf("menuBuildRebuild", "\u91CD\u65B0\u7F16\u8BD1"),
            CreateLeaf("menuBuildRun", "\u8FD0\u884C"));

        var menuDebug = CreateMenu("menuDebug", "\u8C03\u8BD5",
            CreateLeaf("menuDebugStart", "\u5F00\u59CB\u8C03\u8BD5"),
            CreateLeaf("menuDebugStepOver", "\u5355\u6B65\u8DF3\u8FC7"),
            CreateLeaf("menuDebugStepInto", "\u5355\u6B65\u8FDB\u5165"),
            CreateLeaf("menuDebugStop", "\u505C\u6B62\u8C03\u8BD5"));

        var menuTools = CreateMenu("menuTools", "\u5DE5\u5177",
            CreateLeaf("menuToolsCompilerSettings", "\u7F16\u8BD1\u5668\u8BBE\u7F6E"),
            CreateLeaf("menuToolsOptions", "\u9009\u9879"));

        var menuHelp = CreateMenu("menuHelp", "\u5E2E\u52A9",
            CreateLeaf("menuHelpGuide", "\u4F7F\u7528\u8BF4\u660E"),
            CreateLeaf("menuHelpAbout", "\u5173\u4E8E C++Editor"));

        menu.Items.AddRange(new ToolStripItem[]
        {
            menuFile,
            menuEdit,
            menuView,
            menuProject,
            menuBuild,
            menuDebug,
            menuTools,
            menuHelp
        });

        return menu;
    }

    private static ToolStripMenuItem CreateMenu(string name, string text, params ToolStripItem[] children)
    {
        var menu = new ToolStripMenuItem
        {
            Name = name,
            Text = text
        };

        menu.DropDownItems.AddRange(children);
        return menu;
    }

    private static ToolStripMenuItem CreateLeaf(string name, string text)
    {
        return new ToolStripMenuItem
        {
            Name = name,
            Text = text
        };
    }
}

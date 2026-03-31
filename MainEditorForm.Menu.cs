namespace C__Editor;

public partial class MainEditorForm
{
    private ToolStripMenuItem menuFileNew = null!;
    private ToolStripMenuItem menuFileOpen = null!;
    private ToolStripMenuItem menuFileOpenFolder = null!;
    private ToolStripMenuItem menuFileSave = null!;
    private ToolStripMenuItem menuFileSaveAs = null!;
    private ToolStripMenuItem menuFileClose = null!;

    private ToolStripMenuItem menuEditUndo = null!;
    private ToolStripMenuItem menuEditRedo = null!;
    private ToolStripMenuItem menuEditCut = null!;
    private ToolStripMenuItem menuEditCopy = null!;
    private ToolStripMenuItem menuEditPaste = null!;
    private ToolStripMenuItem menuEditFind = null!;
    private ToolStripMenuItem menuEditReplace = null!;
    private ToolStripMenuItem menuEditGoToLine = null!;

    private ToolStripMenuItem menuViewProjectTree = null!;
    private ToolStripMenuItem menuViewOutputWindow = null!;
    private ToolStripMenuItem menuViewResetLayout = null!;

    private ToolStripMenuItem menuProjectNew = null!;
    private ToolStripMenuItem menuProjectOpen = null!;
    private ToolStripMenuItem menuProjectClose = null!;
    private ToolStripMenuItem menuProjectProperties = null!;

    private ToolStripMenuItem menuBuildCompile = null!;
    private ToolStripMenuItem menuBuildRebuild = null!;
    private ToolStripMenuItem menuBuildRun = null!;
    private ToolStripMenuItem menuBuildConfiguration = null!;
    private ToolStripMenuItem menuBuildConfigDebug = null!;
    private ToolStripMenuItem menuBuildConfigRelease = null!;

    private ToolStripMenuItem menuDebugStart = null!;
    private ToolStripMenuItem menuDebugStepOver = null!;
    private ToolStripMenuItem menuDebugStepInto = null!;
    private ToolStripMenuItem menuDebugStop = null!;

    private ToolStripMenuItem menuToolsCompilerSettings = null!;
    private ToolStripMenuItem menuToolsOptions = null!;

    private MenuStrip CreateMainMenu()
    {
        var menu = new MenuStrip
        {
            Name = "menuMain",
            ImageScalingSize = new Size(20, 20),
            TabIndex = 0
        };

        menuFileNew = CreateLeaf("menuFileNew", "新建");
        menuFileNew.Click += (_, _) => NewUntitledDocument();
        RegisterMenuShortcut(EditorCommandIds.FileNew, menuFileNew);

        menuFileOpen = CreateLeaf("menuFileOpen", "打开文件...");
        menuFileOpen.Click += (_, _) => OpenFilesFromDialog();
        RegisterMenuShortcut(EditorCommandIds.FileOpen, menuFileOpen);

        menuFileOpenFolder = CreateLeaf("menuFileOpenFolder", "打开文件夹...");
        menuFileOpenFolder.Click += (_, _) => OpenFolderFromDialog();
        RegisterMenuShortcut(EditorCommandIds.FileOpenFolder, menuFileOpenFolder);

        menuFileSave = CreateLeaf("menuFileSave", "保存");
        menuFileSave.Click += (_, _) => SaveCurrentDocument();
        RegisterMenuShortcut(EditorCommandIds.FileSave, menuFileSave);

        menuFileSaveAs = CreateLeaf("menuFileSaveAs", "另存为...");
        menuFileSaveAs.Click += (_, _) => SaveCurrentDocumentAs();
        RegisterMenuShortcut(EditorCommandIds.FileSaveAs, menuFileSaveAs);

        menuFileClose = CreateLeaf("menuFileClose", "关闭文件");
        menuFileClose.Click += (_, _) => CloseCurrentDocument();
        RegisterMenuShortcut(EditorCommandIds.FileClose, menuFileClose);

        var menuFileExit = CreateLeaf("menuFileExit", "退出");
        menuFileExit.Click += (_, _) => Close();

        var menuFile = CreateMenu("menuFile", "文件",
            menuFileNew,
            menuFileOpen,
            menuFileOpenFolder,
            new ToolStripSeparator(),
            menuFileSave,
            menuFileSaveAs,
            menuFileClose,
            new ToolStripSeparator(),
            menuFileExit);

        menuEditUndo = CreateLeaf("menuEditUndo", "撤销");
        menuEditUndo.Click += (_, _) => UndoInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditUndo, menuEditUndo);

        menuEditRedo = CreateLeaf("menuEditRedo", "重做");
        menuEditRedo.Click += (_, _) => RedoInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditRedo, menuEditRedo);

        menuEditCut = CreateLeaf("menuEditCut", "剪切");
        menuEditCut.Click += (_, _) => CutInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditCut, menuEditCut);

        menuEditCopy = CreateLeaf("menuEditCopy", "复制");
        menuEditCopy.Click += (_, _) => CopyInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditCopy, menuEditCopy);

        menuEditPaste = CreateLeaf("menuEditPaste", "粘贴");
        menuEditPaste.Click += (_, _) => PasteInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditPaste, menuEditPaste);

        var menuEditSelectAll = CreateLeaf("menuEditSelectAll", "全选");
        menuEditSelectAll.Click += (_, _) => SelectAllInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditSelectAll, menuEditSelectAll);

        menuEditFind = CreateLeaf("menuEditFind", "查找...");
        menuEditFind.Click += (_, _) => FindInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditFind, menuEditFind);

        menuEditReplace = CreateLeaf("menuEditReplace", "替换...");
        menuEditReplace.Click += (_, _) => ReplaceInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditReplace, menuEditReplace);

        menuEditGoToLine = CreateLeaf("menuEditGoToLine", "转到行...");
        menuEditGoToLine.Click += (_, _) => GoToLineInEditor();
        RegisterMenuShortcut(EditorCommandIds.EditGoToLine, menuEditGoToLine);

        var menuEdit = CreateMenu("menuEdit", "编辑",
            menuEditUndo,
            menuEditRedo,
            new ToolStripSeparator(),
            menuEditCut,
            menuEditCopy,
            menuEditPaste,
            new ToolStripSeparator(),
            menuEditSelectAll,
            new ToolStripSeparator(),
            menuEditFind,
            menuEditReplace,
            menuEditGoToLine);

        menuViewProjectTree = CreateLeaf("menuViewProjectTree", "显示资源管理器");
        menuViewProjectTree.CheckOnClick = true;
        menuViewProjectTree.Checked = true;
        menuViewProjectTree.CheckedChanged += (_, _) => ToggleProjectTreePanel(menuViewProjectTree.Checked);
        RegisterMenuShortcut(EditorCommandIds.ViewToggleProjectTree, menuViewProjectTree);

        menuViewOutputWindow = CreateLeaf("menuViewOutputWindow", "显示输出窗口");
        menuViewOutputWindow.CheckOnClick = true;
        menuViewOutputWindow.Checked = true;
        menuViewOutputWindow.CheckedChanged += (_, _) => ToggleOutputPanel(menuViewOutputWindow.Checked);
        RegisterMenuShortcut(EditorCommandIds.ViewToggleOutputWindow, menuViewOutputWindow);

        var menuViewCodeStructure = CreateLeaf("menuViewCodeStructure", "显示代码结构");
        menuViewCodeStructure.CheckOnClick = true;
        menuViewCodeStructure.Checked = true;
        menuViewCodeStructure.CheckedChanged += (_, _) => ToggleCodeStructurePanel(menuViewCodeStructure.Checked);

        menuViewResetLayout = CreateLeaf("menuViewResetLayout", "重置布局");
        menuViewResetLayout.Click += (_, _) => ResetMainLayout();
        RegisterMenuShortcut(EditorCommandIds.ViewResetLayout, menuViewResetLayout);

        var menuViewSettings = CreateLeaf("menuViewSettings", "设置");
        menuViewSettings.Click += (_, _) => OpenSettingsDialog();
        RegisterMenuShortcut(EditorCommandIds.ViewOpenSettings, menuViewSettings);

        var menuView = CreateMenu("menuView", "视图",
            menuViewProjectTree,
            menuViewOutputWindow,
            menuViewCodeStructure,
            new ToolStripSeparator(),
            menuViewResetLayout,
            new ToolStripSeparator(),
            menuViewSettings);

        menuProjectNew = CreateLeaf("menuProjectNew", "新建项目");
        menuProjectNew.Click += (_, _) => ShowNotImplemented("新建项目");
        RegisterMenuShortcut(EditorCommandIds.ProjectNew, menuProjectNew);

        menuProjectOpen = CreateLeaf("menuProjectOpen", "打开项目...");
        menuProjectOpen.Click += (_, _) => ShowNotImplemented("打开项目");
        RegisterMenuShortcut(EditorCommandIds.ProjectOpen, menuProjectOpen);

        menuProjectClose = CreateLeaf("menuProjectClose", "关闭项目");
        menuProjectClose.Click += (_, _) => ShowNotImplemented("关闭项目");
        RegisterMenuShortcut(EditorCommandIds.ProjectClose, menuProjectClose);

        menuProjectProperties = CreateLeaf("menuProjectProperties", "项目属性");
        menuProjectProperties.Click += (_, _) => ShowNotImplemented("项目属性");
        RegisterMenuShortcut(EditorCommandIds.ProjectProperties, menuProjectProperties);

        var menuProject = CreateMenu("menuProject", "项目",
            menuProjectNew,
            menuProjectOpen,
            menuProjectClose,
            new ToolStripSeparator(),
            menuProjectProperties);

        menuBuildCompile = CreateLeaf("menuBuildCompile", "编译");
        menuBuildCompile.Click += (_, _) => ExecuteBuildCommand(EditorCommandIds.BuildCompile);
        RegisterMenuShortcut(EditorCommandIds.BuildCompile, menuBuildCompile);

        menuBuildRebuild = CreateLeaf("menuBuildRebuild", "重新编译");
        menuBuildRebuild.Click += (_, _) => ExecuteBuildCommand(EditorCommandIds.BuildRebuild);
        RegisterMenuShortcut(EditorCommandIds.BuildRebuild, menuBuildRebuild);

        menuBuildRun = CreateLeaf("menuBuildRun", "运行");
        menuBuildRun.Click += (_, _) => ExecuteBuildCommand(EditorCommandIds.BuildRun);
        RegisterMenuShortcut(EditorCommandIds.BuildRun, menuBuildRun);

        // Build Configuration submenu
        menuBuildConfigDebug = CreateLeaf("menuBuildConfigDebug", "Debug");
        menuBuildConfigDebug.CheckOnClick = true;
        menuBuildConfigDebug.Checked = true;
        menuBuildConfigDebug.Click += (_, _) => SetBuildConfiguration(BuildConfiguration.Debug);

        menuBuildConfigRelease = CreateLeaf("menuBuildConfigRelease", "Release");
        menuBuildConfigRelease.CheckOnClick = true;
        menuBuildConfigRelease.Checked = false;
        menuBuildConfigRelease.Click += (_, _) => SetBuildConfiguration(BuildConfiguration.Release);

        menuBuildConfiguration = CreateMenu("menuBuildConfiguration", "构建配置",
            menuBuildConfigDebug,
            menuBuildConfigRelease);

        var menuInsertSnippet = CreateLeaf("menuInsertSnippet", "插入代码片段...");
        menuInsertSnippet.Click += (_, _) => ShowCodeSnippetDialog();
        RegisterMenuShortcut(EditorCommandIds.EditInsertSnippet, menuInsertSnippet);

        var menuBuild = CreateMenu("menuBuild", "编译",
            menuBuildCompile,
            menuBuildRebuild,
            new ToolStripSeparator(),
            menuBuildRun,
            new ToolStripSeparator(),
            menuBuildConfiguration);

        menuDebugStart = CreateLeaf("menuDebugStart", "开始调试");
        menuDebugStart.Click += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStart);
        RegisterMenuShortcut(EditorCommandIds.DebugStart, menuDebugStart);

        menuDebugStepOver = CreateLeaf("menuDebugStepOver", "单步跳过");
        menuDebugStepOver.Click += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStepOver);
        RegisterMenuShortcut(EditorCommandIds.DebugStepOver, menuDebugStepOver);

        menuDebugStepInto = CreateLeaf("menuDebugStepInto", "单步进入");
        menuDebugStepInto.Click += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStepInto);
        RegisterMenuShortcut(EditorCommandIds.DebugStepInto, menuDebugStepInto);

        menuDebugStop = CreateLeaf("menuDebugStop", "停止调试");
        menuDebugStop.Click += (_, _) => ExecuteDebugCommand(EditorCommandIds.DebugStop);
        RegisterMenuShortcut(EditorCommandIds.DebugStop, menuDebugStop);

        var menuDebug = CreateMenu("menuDebug", "调试",
            menuDebugStart,
            menuDebugStepOver,
            menuDebugStepInto,
            menuDebugStop);

        menuToolsCompilerSettings = CreateLeaf("menuToolsCompilerSettings", "编译器设置");
        menuToolsCompilerSettings.Click += (_, _) => OpenCompilerSettingsDialog();

        menuToolsOptions = CreateLeaf("menuToolsOptions", "选项");
        menuToolsOptions.Click += (_, _) => OpenSettingsDialog();
        RegisterMenuShortcut(EditorCommandIds.ViewOpenSettings, menuToolsOptions);

        var menuTools = CreateMenu("menuTools", "工具",
            menuToolsCompilerSettings,
            new ToolStripSeparator(),
            menuToolsOptions);

        var menuHelp = CreateMenu("menuHelp", "帮助",
            CreateLeaf("menuHelpGuide", "使用说明"),
            CreateLeaf("menuHelpAbout", "关于 C++Editor"));

        ((ToolStripMenuItem)menuHelp.DropDownItems[0]).Click += (_, _) => ShowUsageGuide();
        ((ToolStripMenuItem)menuHelp.DropDownItems[1]).Click += (_, _) => ShowAboutDialog();

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

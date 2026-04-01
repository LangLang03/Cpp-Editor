namespace C__Editor;

internal static class EditorCommandIds
{
    public const string FileNew = "file.new";
    public const string FileOpen = "file.open";
    public const string FileOpenFolder = "file.openFolder";
    public const string FileSave = "file.save";
    public const string FileSaveAs = "file.saveAs";
    public const string FileClose = "file.close";

    public const string EditUndo = "edit.undo";
    public const string EditRedo = "edit.redo";
    public const string EditCut = "edit.cut";
    public const string EditCopy = "edit.copy";
    public const string EditPaste = "edit.paste";
    public const string EditSelectAll = "edit.selectAll";
    public const string EditFind = "edit.find";
    public const string EditReplace = "edit.replace";
    public const string EditGoToLine = "edit.goToLine";
    public const string EditInsertSnippet = "edit.insertSnippet";

    public const string ViewToggleProjectTree = "view.toggleProjectTree";
    public const string ViewToggleOutputWindow = "view.toggleOutputWindow";
    public const string ViewResetLayout = "view.resetLayout";
    public const string ViewOpenSettings = "view.openSettings";

    public const string ProjectNew = "project.new";
    public const string ProjectOpen = "project.open";
    public const string ProjectClose = "project.close";
    public const string ProjectProperties = "project.properties";

    public const string BuildCompile = "build.compile";
    public const string BuildRebuild = "build.rebuild";
    public const string BuildRun = "build.run";

    public const string DebugStart = "debug.start";
    public const string DebugStepOver = "debug.stepOver";
    public const string DebugStepInto = "debug.stepInto";
    public const string DebugStepOut = "debug.stepOut";
    public const string DebugStop = "debug.stop";

    public const string ExplorerNewFile = "explorer.newFile";
    public const string ExplorerNewFolder = "explorer.newFolder";
    public const string ExplorerCopy = "explorer.copy";
    public const string ExplorerPaste = "explorer.paste";
    public const string ExplorerRename = "explorer.rename";
    public const string ExplorerDelete = "explorer.delete";
    public const string ExplorerRefresh = "explorer.refresh";
}

internal sealed class ShortcutCommandDefinition
{
    public ShortcutCommandDefinition(string commandId, string category, string displayName, string defaultGesture)
    {
        CommandId = commandId;
        Category = category;
        DisplayName = displayName;
        DefaultGesture = defaultGesture;
    }

    public string CommandId { get; }

    public string Category { get; }

    public string DisplayName { get; }

    public string DefaultGesture { get; }
}

internal static class EditorShortcutCatalog
{
    private static readonly IReadOnlyList<ShortcutCommandDefinition> Definitions = new[]
    {
        new ShortcutCommandDefinition(EditorCommandIds.FileNew, "文件", "新建文件", "Ctrl+N"),
        new ShortcutCommandDefinition(EditorCommandIds.FileOpen, "文件", "打开文件", "Ctrl+O"),
        new ShortcutCommandDefinition(EditorCommandIds.FileOpenFolder, "文件", "打开文件夹", "Ctrl+Shift+O"),
        new ShortcutCommandDefinition(EditorCommandIds.FileSave, "文件", "保存", "Ctrl+S"),
        new ShortcutCommandDefinition(EditorCommandIds.FileSaveAs, "文件", "另存为", "Ctrl+Shift+S"),
        new ShortcutCommandDefinition(EditorCommandIds.FileClose, "文件", "关闭文件", "Ctrl+W"),

        new ShortcutCommandDefinition(EditorCommandIds.EditUndo, "编辑", "撤销", "Ctrl+Z"),
        new ShortcutCommandDefinition(EditorCommandIds.EditRedo, "编辑", "重做", "Ctrl+Y"),
        new ShortcutCommandDefinition(EditorCommandIds.EditCut, "编辑", "剪切", "Ctrl+X"),
        new ShortcutCommandDefinition(EditorCommandIds.EditCopy, "编辑", "复制", "Ctrl+C"),
        new ShortcutCommandDefinition(EditorCommandIds.EditPaste, "编辑", "粘贴", "Ctrl+V"),
        new ShortcutCommandDefinition(EditorCommandIds.EditSelectAll, "编辑", "全选", "Ctrl+A"),
        new ShortcutCommandDefinition(EditorCommandIds.EditFind, "编辑", "查找", "Ctrl+F"),
        new ShortcutCommandDefinition(EditorCommandIds.EditReplace, "编辑", "替换", "Ctrl+H"),
        new ShortcutCommandDefinition(EditorCommandIds.EditGoToLine, "编辑", "转到行", "Ctrl+G"),
        new ShortcutCommandDefinition(EditorCommandIds.EditInsertSnippet, "编辑", "插入代码片段", "Ctrl+Shift+S"),

        new ShortcutCommandDefinition(EditorCommandIds.ViewToggleProjectTree, "视图", "切换资源管理器", "Ctrl+B"),
        new ShortcutCommandDefinition(EditorCommandIds.ViewToggleOutputWindow, "视图", "切换输出窗口", "Ctrl+J"),
        new ShortcutCommandDefinition(EditorCommandIds.ViewResetLayout, "视图", "重置布局", "Ctrl+Shift+R"),
        new ShortcutCommandDefinition(EditorCommandIds.ViewOpenSettings, "视图", "打开设置", "Ctrl+,"),

        new ShortcutCommandDefinition(EditorCommandIds.ProjectNew, "项目", "新建项目", "Ctrl+Shift+N"),
        new ShortcutCommandDefinition(EditorCommandIds.ProjectOpen, "项目", "打开项目", "Ctrl+Alt+O"),
        new ShortcutCommandDefinition(EditorCommandIds.ProjectClose, "项目", "关闭项目", string.Empty),
        new ShortcutCommandDefinition(EditorCommandIds.ProjectProperties, "项目", "项目属性", "Alt+Enter"),

        new ShortcutCommandDefinition(EditorCommandIds.BuildCompile, "编译", "编译", "F7"),
        new ShortcutCommandDefinition(EditorCommandIds.BuildRebuild, "编译", "重新编译", "Ctrl+Shift+B"),
        new ShortcutCommandDefinition(EditorCommandIds.BuildRun, "编译", "运行", "Ctrl+F5"),

        new ShortcutCommandDefinition(EditorCommandIds.DebugStart, "调试", "开始调试", "F5"),
        new ShortcutCommandDefinition(EditorCommandIds.DebugStepOver, "调试", "单步跳过", "F10"),
        new ShortcutCommandDefinition(EditorCommandIds.DebugStepInto, "调试", "单步进入", "F11"),
        new ShortcutCommandDefinition(EditorCommandIds.DebugStepOut, "调试", "单步跳出", "Shift+F11"),
        new ShortcutCommandDefinition(EditorCommandIds.DebugStop, "调试", "停止调试", "Shift+F5"),

        // VSCode-like baseline: explorer actions are mostly context menu driven by default.
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerNewFile, "资源管理器", "新建文件", string.Empty),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerNewFolder, "资源管理器", "新建文件夹", string.Empty),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerCopy, "资源管理器", "复制", string.Empty),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerPaste, "资源管理器", "粘贴", string.Empty),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerRename, "资源管理器", "重命名", "F2"),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerDelete, "资源管理器", "删除", "Delete"),
        new ShortcutCommandDefinition(EditorCommandIds.ExplorerRefresh, "资源管理器", "刷新", string.Empty)
    };

    internal static IReadOnlyList<ShortcutCommandDefinition> GetDefinitions()
    {
        return Definitions;
    }

    internal static IReadOnlyDictionary<string, string> CreateDefaultGestureMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            result[definition.CommandId] = definition.DefaultGesture;
        }

        return result;
    }
}

namespace C__Editor;

public partial class MainEditorForm
{
    private static readonly object LazyPlaceholderTag = new();

    private ContextMenuStrip projectTreeContextMenu = null!;
    private ToolStripMenuItem contextOpenFolder = null!;
    private ToolStripMenuItem contextOpenFile = null!;
    private ToolStripMenuItem contextNewFile = null!;
    private ToolStripMenuItem contextNewFolder = null!;
    private ToolStripMenuItem contextNewCppFile = null!;
    private ToolStripMenuItem contextNewHppFile = null!;
    private ToolStripMenuItem contextNewCFile = null!;
    private ToolStripMenuItem contextNewHFile = null!;
    private ToolStripMenuItem contextCopy = null!;
    private ToolStripMenuItem contextPaste = null!;
    private ToolStripMenuItem contextRename = null!;
    private ToolStripMenuItem contextDelete = null!;
    private ToolStripMenuItem contextRefresh = null!;

    private readonly List<string> copiedNodePaths = new();
    private readonly List<TreeNode> selectedExplorerNodes = new();
    private TreeNode? explorerSelectionAnchorNode;
    private bool isEditingTreeLabel;

    [global::System.Runtime.InteropServices.DllImport("user32.dll", CharSet = global::System.Runtime.InteropServices.CharSet.Auto)]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private const int TVM_GETEDITCONTROL = 0x110F;
    private const int EM_SETSEL = 0x00B1;

    private enum ExplorerNodeKind
    {
        CommandOpenFolder,
        CommandOpenFile,
        Directory,
        File
    }

    private sealed class ExplorerNodeData
    {
        public ExplorerNodeData(ExplorerNodeKind kind, string? fullPath = null)
        {
            Kind = kind;
            FullPath = fullPath;
        }

        public ExplorerNodeKind Kind { get; }

        public string? FullPath { get; set; }
    }

    private TreeView CreateProjectTree()
    {
        var projectTree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            LabelEdit = true,
            DrawMode = TreeViewDrawMode.OwnerDrawText,
            Name = "treeProject",
            TabIndex = 0
        };

        projectTree.BeforeExpand += TreeProject_BeforeExpand;
        projectTree.NodeMouseDoubleClick += TreeProject_NodeMouseDoubleClick;
        projectTree.NodeMouseClick += TreeProject_NodeMouseClick;
        projectTree.BeforeLabelEdit += TreeProject_BeforeLabelEdit;
        projectTree.AfterLabelEdit += TreeProject_AfterLabelEdit;
        projectTree.KeyDown += TreeProject_KeyDown;
        projectTree.MouseUp += TreeProject_MouseUp;
        projectTree.DrawNode += TreeProject_DrawNode;

        projectTreeContextMenu = CreateProjectTreeContextMenu();
        projectTree.ContextMenuStrip = projectTreeContextMenu;

        // Bind field before calling selection helpers used by initial reset.
        treeProject = projectTree;
        ResetTreeToQuickOpen(projectTree);
        return projectTree;
    }

    private void ResetTreeToQuickOpen(TreeView projectTree)
    {
        projectTree.Nodes.Clear();
        projectTree.Nodes.Add(CreateCommandNode("\u6253\u5F00\u6587\u4EF6\u5939...", ExplorerNodeKind.CommandOpenFolder));
        projectTree.Nodes.Add(CreateCommandNode("\u6253\u5F00\u6587\u4EF6...", ExplorerNodeKind.CommandOpenFile));
        SelectSingleExplorerNode(null);
    }

    private ContextMenuStrip CreateProjectTreeContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        contextOpenFolder = new ToolStripMenuItem("\u6253\u5F00\u6587\u4EF6\u5939...", null, (_, _) => OpenFolderFromDialog());
        contextOpenFile = new ToolStripMenuItem("\u6253\u5F00\u6587\u4EF6...", null, (_, _) => OpenFilesFromDialog());
        contextNewFile = new ToolStripMenuItem("\u65B0\u5EFA\u6587\u4EF6", null, (_, _) => CreateGeneralFile());
        contextNewFolder = new ToolStripMenuItem("\u65B0\u5EFA\u6587\u4EF6\u5939", null, (_, _) => CreateFolder());
        contextNewCppFile = new ToolStripMenuItem("\u65B0\u5EFA C++ \u6E90\u6587\u4EF6 (.cpp)", null, (_, _) => CreateCppFile());
        contextNewHppFile = new ToolStripMenuItem("\u65B0\u5EFA C++ \u5934\u6587\u4EF6 (.hpp)", null, (_, _) => CreateHppFile());
        contextNewCFile = new ToolStripMenuItem("\u65B0\u5EFA C \u6E90\u6587\u4EF6 (.c)", null, (_, _) => CreateCFile());
        contextNewHFile = new ToolStripMenuItem("\u65B0\u5EFA C \u5934\u6587\u4EF6 (.h)", null, (_, _) => CreateHFile());
        contextCopy = new ToolStripMenuItem("\u590D\u5236", null, (_, _) => CopySelectedNode());
        contextPaste = new ToolStripMenuItem("\u7C98\u8D34", null, (_, _) => PasteIntoSelectedLocation());
        contextRename = new ToolStripMenuItem("\u91CD\u547D\u540D", null, (_, _) => BeginRenameSelectedNode());
        contextDelete = new ToolStripMenuItem("\u5220\u9664", null, (_, _) => DeleteSelectedNode());
        contextRefresh = new ToolStripMenuItem("\u5237\u65B0", null, (_, _) => RefreshSelectedNode());

        contextMenu.Items.AddRange(new ToolStripItem[]
        {
            contextOpenFolder,
            contextOpenFile,
            new ToolStripSeparator(),
            contextNewFile,
            contextNewFolder,
            contextNewCppFile,
            contextNewHppFile,
            contextNewCFile,
            contextNewHFile,
            new ToolStripSeparator(),
            contextCopy,
            contextPaste,
            contextRename,
            contextDelete,
            new ToolStripSeparator(),
            contextRefresh
        });

        contextMenu.Opening += ProjectTreeContextMenu_Opening;
        ApplyProjectTreeShortcutDisplayStrings();
        return contextMenu;
    }

    private void ApplyProjectTreeShortcutDisplayStrings()
    {
        if (contextOpenFolder is null)
        {
            return;
        }

        contextOpenFolder.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.FileOpenFolder);
        contextOpenFile.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.FileOpen);
        contextNewFile.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerNewFile);
        contextNewFolder.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerNewFolder);
        contextCopy.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerCopy);
        contextPaste.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerPaste);
        contextRename.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerRename);
        contextDelete.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerDelete);
        contextRefresh.ShortcutKeyDisplayString = GetShortcutDisplayText(EditorCommandIds.ExplorerRefresh);
    }

    private TreeNode CreateCommandNode(string text, ExplorerNodeKind kind)
    {
        return new TreeNode(text)
        {
            Tag = new ExplorerNodeData(kind)
        };
    }

    private TreeNode CreateDirectoryNode(string directoryPath)
    {
        var node = new TreeNode(GetDisplayName(directoryPath))
        {
            Tag = new ExplorerNodeData(ExplorerNodeKind.Directory, directoryPath)
        };

        if (DirectoryHasChildren(directoryPath))
        {
            node.Nodes.Add(new TreeNode("\u52A0\u8F7D\u4E2D...")
            {
                Tag = LazyPlaceholderTag
            });
        }

        return node;
    }

    private TreeNode CreateFileNode(string filePath)
    {
        return new TreeNode(GetDisplayName(filePath))
        {
            Tag = new ExplorerNodeData(ExplorerNodeKind.File, filePath)
        };
    }

    private static bool DirectoryHasChildren(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directoryPath).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string GetDisplayName(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        return string.IsNullOrWhiteSpace(name) ? fullPath : name;
    }

    private static ExplorerNodeData? GetNodeData(TreeNode? node)
    {
        return node?.Tag as ExplorerNodeData;
    }

    private static bool IsFileSystemNode(TreeNode? node)
    {
        var kind = GetNodeData(node)?.Kind;
        return kind is ExplorerNodeKind.Directory or ExplorerNodeKind.File;
    }

    private static bool IsLazyPlaceholderNode(TreeNode? node)
    {
        return node?.Tag == LazyPlaceholderTag;
    }

    private static void ConsumeKey(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private IReadOnlyList<TreeNode> GetExplorerSelectedNodes()
    {
        var nodes = selectedExplorerNodes
            .Where(node => node.TreeView == treeProject)
            .Distinct()
            .ToList();

        if (nodes.Count == 0 && treeProject.SelectedNode is not null)
        {
            nodes.Add(treeProject.SelectedNode);
        }

        return nodes;
    }

    private IReadOnlyList<TreeNode> GetSelectedFileSystemNodes()
    {
        return GetExplorerSelectedNodes()
            .Where(IsFileSystemNode)
            .ToList();
    }

    private void SelectSingleExplorerNode(TreeNode? node)
    {
        selectedExplorerNodes.Clear();

        if (node is not null)
        {
            selectedExplorerNodes.Add(node);
            explorerSelectionAnchorNode = node;
            treeProject.SelectedNode = node;
        }
        else
        {
            explorerSelectionAnchorNode = null;
            treeProject.SelectedNode = null;
        }

        treeProject.Invalidate();
    }

    private void ToggleExplorerNodeSelection(TreeNode node)
    {
        var existingIndex = selectedExplorerNodes.FindIndex(item => item == node);
        if (existingIndex >= 0)
        {
            selectedExplorerNodes.RemoveAt(existingIndex);
            if (selectedExplorerNodes.Count == 0)
            {
                treeProject.SelectedNode = null;
            }
            else if (treeProject.SelectedNode is null || !selectedExplorerNodes.Contains(treeProject.SelectedNode))
            {
                treeProject.SelectedNode = selectedExplorerNodes[^1];
            }
        }
        else
        {
            selectedExplorerNodes.Add(node);
            treeProject.SelectedNode = node;
            explorerSelectionAnchorNode ??= node;
        }

        treeProject.Invalidate();
    }

    private void SelectExplorerNodeRange(TreeNode targetNode)
    {
        var anchor = explorerSelectionAnchorNode ?? treeProject.SelectedNode ?? targetNode;
        var visibleNodes = EnumerateVisibleNodes();
        var anchorIndex = visibleNodes.IndexOf(anchor);
        var targetIndex = visibleNodes.IndexOf(targetNode);
        if (anchorIndex < 0 || targetIndex < 0)
        {
            SelectSingleExplorerNode(targetNode);
            return;
        }

        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);

        selectedExplorerNodes.Clear();
        for (var i = start; i <= end; i++)
        {
            selectedExplorerNodes.Add(visibleNodes[i]);
        }

        treeProject.SelectedNode = targetNode;
        treeProject.Invalidate();
    }

    private List<TreeNode> EnumerateVisibleNodes()
    {
        var result = new List<TreeNode>();
        foreach (TreeNode rootNode in treeProject.Nodes)
        {
            AppendVisibleNodes(rootNode, result);
        }

        return result;
    }

    private static void AppendVisibleNodes(TreeNode node, List<TreeNode> sink)
    {
        sink.Add(node);
        if (!node.IsExpanded)
        {
            return;
        }

        foreach (TreeNode child in node.Nodes)
        {
            AppendVisibleNodes(child, sink);
        }
    }

    private bool IsExplorerNodeSelected(TreeNode node)
    {
        return selectedExplorerNodes.Contains(node);
    }

    private void UpdateSelectionFromNodeClick(TreeNode node, MouseButtons button)
    {
        var control = (ModifierKeys & Keys.Control) == Keys.Control;
        var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

        if (shift)
        {
            SelectExplorerNodeRange(node);
            return;
        }

        if (control)
        {
            ToggleExplorerNodeSelection(node);
            return;
        }

        if (button == MouseButtons.Right && IsExplorerNodeSelected(node))
        {
            treeProject.SelectedNode = node;
            treeProject.Invalidate();
            return;
        }

        SelectSingleExplorerNode(node);
    }

    private void TreeProject_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        var selected = IsExplorerNodeSelected(e.Node);
        var backColor = selected
            ? BlendColor(treeProject.BackColor, treeProject.ForeColor, treeProject.Focused ? 0.24d : 0.14d)
            : treeProject.BackColor;
        var foreColor = treeProject.ForeColor;

        using var backBrush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            treeProject.Font,
            e.Bounds,
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (selected && treeProject.Focused)
        {
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, foreColor, backColor);
        }
    }

    private void OpenFolderFromDialog()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "\u9009\u62E9\u8981\u6253\u5F00\u7684\u6587\u4EF6\u5939",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
        {
            AddOpenedFolderNode(dialog.SelectedPath, beginEdit: false);
        }
    }

    private void OpenFilesFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "\u9009\u62E9\u8981\u6253\u5F00\u7684\u6587\u4EF6",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        foreach (var filePath in dialog.FileNames.Where(File.Exists))
        {
            AddOpenedFileNode(filePath, beginEdit: false);
            ShowFileInEditorPlaceholder(filePath);
        }
    }

    private void AddOpenedFolderNode(string folderPath, bool beginEdit)
    {
        var existing = FindNodeByPath(folderPath);
        if (existing is not null)
        {
            SelectSingleExplorerNode(existing);
            existing.EnsureVisible();
            return;
        }

        var folderNode = CreateDirectoryNode(folderPath);
        treeProject.Nodes.Add(folderNode);
        SelectSingleExplorerNode(folderNode);
        folderNode.Expand();

        if (beginEdit)
        {
            BeginExplorerNodeRename(folderNode);
        }
    }

    private void AddOpenedFileNode(string filePath, bool beginEdit)
    {
        var existing = FindNodeByPath(filePath);
        if (existing is not null)
        {
            SelectSingleExplorerNode(existing);
            existing.EnsureVisible();
            return;
        }

        var fileNode = CreateFileNode(filePath);
        treeProject.Nodes.Add(fileNode);
        SelectSingleExplorerNode(fileNode);
        fileNode.EnsureVisible();

        if (beginEdit)
        {
            BeginExplorerNodeRename(fileNode);
        }
    }

    private void LoadDirectoryChildren(TreeNode directoryNode, string directoryPath)
    {
        directoryNode.Nodes.Clear();

        try
        {
            var directories = Directory.GetDirectories(directoryPath)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
            foreach (var childDirectory in directories)
            {
                directoryNode.Nodes.Add(CreateDirectoryNode(childDirectory));
            }

            var files = Directory.GetFiles(directoryPath)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
            foreach (var childFile in files)
            {
                directoryNode.Nodes.Add(CreateFileNode(childFile));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u65E0\u6CD5\u8BFB\u53D6\u76EE\u5F55", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private TreeNode? FindNodeByPath(string path)
    {
        foreach (TreeNode rootNode in treeProject.Nodes)
        {
            var node = FindNodeByPathRecursive(rootNode, path);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    private static TreeNode? FindNodeByPathRecursive(TreeNode node, string path)
    {
        var nodeData = GetNodeData(node);
        if (nodeData?.FullPath is not null &&
            string.Equals(Path.GetFullPath(nodeData.FullPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (TreeNode childNode in node.Nodes)
        {
            var match = FindNodeByPathRecursive(childNode, path);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private string? GetTargetDirectory(TreeNode? referenceNode)
    {
        referenceNode ??= GetExplorerSelectedNodes().LastOrDefault();

        var nodeData = GetNodeData(referenceNode);
        if (nodeData?.Kind == ExplorerNodeKind.Directory && nodeData.FullPath is not null)
        {
            return nodeData.FullPath;
        }

        if (nodeData?.Kind == ExplorerNodeKind.File && nodeData.FullPath is not null)
        {
            return Path.GetDirectoryName(nodeData.FullPath);
        }

        foreach (TreeNode rootNode in treeProject.Nodes)
        {
            var rootData = GetNodeData(rootNode);
            if (rootData?.Kind == ExplorerNodeKind.Directory && rootData.FullPath is not null)
            {
                return rootData.FullPath;
            }
        }

        return null;
    }

    private IReadOnlyList<string> GetClipboardSourcePaths()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var copiedPath in copiedNodePaths)
        {
            if (string.IsNullOrWhiteSpace(copiedPath))
            {
                continue;
            }

            if ((File.Exists(copiedPath) || Directory.Exists(copiedPath)) && seen.Add(copiedPath))
            {
                result.Add(copiedPath);
            }
        }

        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var fileDropList = Clipboard.GetFileDropList();
                foreach (var item in fileDropList.Cast<string>())
                {
                    if ((File.Exists(item) || Directory.Exists(item)) && seen.Add(item))
                    {
                        result.Add(item);
                    }
                }
            }
        }
        catch
        {
            // Ignore clipboard access failures and fall back to internal clipboard.
        }

        return result;
    }

    private void CreateGeneralFile()
    {
        CreateNewFileInTarget("new_file.txt");
    }

    private void CreateFolder()
    {
        CreateNewDirectoryInTarget("new_folder");
    }

    private void CreateCppFile()
    {
        CreateNewFileInTarget("new_file.cpp");
    }

    private void CreateHppFile()
    {
        CreateNewFileInTarget("new_file.hpp");
    }

    private void CreateCFile()
    {
        CreateNewFileInTarget("new_file.c");
    }

    private void CreateHFile()
    {
        CreateNewFileInTarget("new_file.h");
    }

    private string ResolveTemplateForNewFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cpp" or ".cc" or ".cxx" => cppTemplateSettings.CppSourceTemplate ?? string.Empty,
            ".hpp" or ".hh" or ".hxx" => cppTemplateSettings.CppHeaderTemplate ?? string.Empty,
            ".c" => cppTemplateSettings.CSourceTemplate ?? string.Empty,
            ".h" => cppTemplateSettings.CHeaderTemplate ?? string.Empty,
            _ => cppTemplateSettings.OtherFileTemplate ?? string.Empty
        };
    }

    private void CreateNewFileInTarget(string defaultFileName)
    {
        var targetDirectory = GetTargetDirectory(treeProject.SelectedNode);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            MessageBox.Show(this, "\u8BF7\u5148\u9009\u62E9\u76EE\u6807\u6587\u4EF6\u5939\uFF08\u6216\u5148\u6253\u5F00\u4E00\u4E2A\u6587\u4EF6\u5939\uFF09\u3002", "\u65E0\u53EF\u7528\u76EE\u5F55", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var createdPath = BuildUniquePath(targetDirectory, defaultFileName);
            var template = ResolveTemplateForNewFile(createdPath);
            File.WriteAllText(createdPath, template, new System.Text.UTF8Encoding(false));

            RevealPath(createdPath, beginEdit: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u65B0\u5EFA\u6587\u4EF6\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CreateNewDirectoryInTarget(string defaultFolderName)
    {
        var targetDirectory = GetTargetDirectory(treeProject.SelectedNode);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            MessageBox.Show(this, "\u8BF7\u5148\u9009\u62E9\u76EE\u6807\u6587\u4EF6\u5939\uFF08\u6216\u5148\u6253\u5F00\u4E00\u4E2A\u6587\u4EF6\u5939\uFF09\u3002", "\u65E0\u53EF\u7528\u76EE\u5F55", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var createdPath = BuildUniquePath(targetDirectory, defaultFolderName);
            Directory.CreateDirectory(createdPath);
            RevealPath(createdPath, beginEdit: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u65B0\u5EFA\u6587\u4EF6\u5939\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RevealPath(string fullPath, bool beginEdit)
    {
        var existingNode = FindNodeByPath(fullPath);
        if (existingNode is not null)
        {
            SelectSingleExplorerNode(existingNode);
            existingNode.EnsureVisible();
            if (beginEdit)
            {
                BeginExplorerNodeRename(existingNode);
            }

            return;
        }

        var parentPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            var parentNode = FindNodeByPath(parentPath);
            var parentData = GetNodeData(parentNode);
            if (parentNode is not null && parentData?.Kind == ExplorerNodeKind.Directory)
            {
                LoadDirectoryChildren(parentNode, parentPath);
                parentNode.Expand();
                existingNode = FindNodeByPath(fullPath);
                if (existingNode is not null)
                {
                    SelectSingleExplorerNode(existingNode);
                    existingNode.EnsureVisible();
                    if (beginEdit)
                    {
                        BeginExplorerNodeRename(existingNode);
                    }

                    return;
                }
            }
        }

        if (Directory.Exists(fullPath))
        {
            AddOpenedFolderNode(fullPath, beginEdit);
            return;
        }

        if (File.Exists(fullPath))
        {
            AddOpenedFileNode(fullPath, beginEdit);
        }
    }

    private static string BuildUniquePath(string directoryPath, string baseName)
    {
        var candidate = Path.Combine(directoryPath, baseName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(baseName);
        var extension = Path.GetExtension(baseName);
        var index = 1;

        while (true)
        {
            var suffix = index == 1 ? " - \u526F\u672C" : $" - \u526F\u672C {index}";
            candidate = Path.Combine(directoryPath, $"{nameWithoutExtension}{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private void CopySelectedNode()
    {
        var selectedPaths = GetSelectedFileSystemNodes()
            .Select(node => GetNodeData(node)?.FullPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedPaths.Count == 0)
        {
            return;
        }

        copiedNodePaths.Clear();
        copiedNodePaths.AddRange(selectedPaths);

        try
        {
            var fileDropList = new System.Collections.Specialized.StringCollection();
            foreach (var selectedPath in selectedPaths)
            {
                fileDropList.Add(selectedPath);
            }

            Clipboard.SetFileDropList(fileDropList);
        }
        catch
        {
            // Internal clipboard still works even if system clipboard is unavailable.
        }
    }

    private void PasteIntoSelectedLocation()
    {
        var sourcePaths = GetClipboardSourcePaths();
        if (sourcePaths.Count == 0)
        {
            return;
        }

        var targetDirectory = GetTargetDirectory(treeProject.SelectedNode);
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            MessageBox.Show(this, "\u8BF7\u5148\u9009\u4E2D\u4E00\u4E2A\u53EF\u7C98\u8D34\u7684\u6587\u4EF6\u5939\u3002", "\u7C98\u8D34", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            foreach (var sourcePath in sourcePaths)
            {
                if (Directory.Exists(sourcePath))
                {
                    if (IsInsideDirectory(targetDirectory, sourcePath))
                    {
                        MessageBox.Show(this, "\u4E0D\u80FD\u5C06\u6587\u4EF6\u5939\u7C98\u8D34\u5230\u5176\u81EA\u8EAB\u6216\u5B50\u76EE\u5F55\u4E2D\u3002", "\u7C98\u8D34\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    var targetPath = BuildUniquePath(targetDirectory, Path.GetFileName(sourcePath));
                    CopyDirectory(sourcePath, targetPath);
                    RevealPath(targetPath, beginEdit: false);
                    continue;
                }

                if (File.Exists(sourcePath))
                {
                    var targetPath = BuildUniquePath(targetDirectory, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, targetPath);
                    RevealPath(targetPath, beginEdit: false);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u7C98\u8D34\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsInsideDirectory(string childPath, string parentPath)
    {
        var normalizedChild = Path.GetFullPath(childPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, destinationFilePath);
        }

        foreach (var childDirectory in Directory.GetDirectories(sourceDirectory))
        {
            var destinationChildDirectory = Path.Combine(destinationDirectory, Path.GetFileName(childDirectory));
            CopyDirectory(childDirectory, destinationChildDirectory);
        }
    }

    private void BeginExplorerNodeRename(TreeNode node)
    {
        if (!IsFileSystemNode(node))
        {
            return;
        }

        SelectSingleExplorerNode(node);
        node.BeginEdit();

        if (!explorerSettings.RenameSelectNameOnly)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (treeProject.IsDisposed || !node.IsEditing)
            {
                return;
            }

            var editHandle = SendMessage(treeProject.Handle, TVM_GETEDITCONTROL, 0, 0);
            if (editHandle == 0)
            {
                return;
            }

            var text = node.Text ?? string.Empty;
            var selectEnd = text.Length;
            var nodeData = GetNodeData(node);
            if (nodeData?.Kind == ExplorerNodeKind.File)
            {
                var extension = Path.GetExtension(text);
                if (!string.IsNullOrEmpty(extension))
                {
                    selectEnd = Math.Max(0, text.Length - extension.Length);
                }
            }

            SendMessage(editHandle, EM_SETSEL, 0, selectEnd);
        }));
    }

    private void BeginRenameSelectedNode()
    {
        var nodes = GetSelectedFileSystemNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        if (nodes.Count > 1)
        {
            MessageBox.Show(this, "多选时无法重命名，请只选择一个文件或文件夹。", "重命名", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        BeginExplorerNodeRename(nodes[0]);
    }

    private void DeleteSelectedNode()
    {
        var selectedNodes = GetSelectedFileSystemNodes();
        if (selectedNodes.Count == 0)
        {
            return;
        }

        if (selectedNodes.Any(node =>
            GetNodeData(node)?.Kind == ExplorerNodeKind.Directory &&
            node.Parent is null))
        {
            MessageBox.Show(this, "不允许删除根目录节点。", "删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var nodeTypeText = selectedNodes.Count > 1 ? "选中项" : (GetNodeData(selectedNodes[0])?.Kind == ExplorerNodeKind.Directory ? "\u6587\u4EF6\u5939" : "\u6587\u4EF6");
        var nodeDisplay = selectedNodes.Count > 1 ? $"{selectedNodes.Count} 项" : $"\"{selectedNodes[0].Text}\"";
        var result = MessageBox.Show(
            this,
            $"\u786E\u5B9A\u5220\u9664{nodeTypeText} {nodeDisplay} \u5417\uFF1F",
            "\u786E\u8BA4\u5220\u9664",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var sortedNodes = selectedNodes
                .Select(node => new { Node = node, Data = GetNodeData(node) })
                .Where(item => !string.IsNullOrWhiteSpace(item.Data?.FullPath))
                .OrderByDescending(item => item.Data!.FullPath!.Length)
                .ToList();

            foreach (var item in sortedNodes)
            {
                var data = item.Data!;
                var fullPath = data.FullPath!;

                if (data.Kind == ExplorerNodeKind.Directory && Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }
                else if (data.Kind == ExplorerNodeKind.File && File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }

            foreach (var node in selectedNodes.OrderByDescending(node => node.Level))
            {
                if (node.TreeView == treeProject)
                {
                    node.Remove();
                }
            }

            SelectSingleExplorerNode(treeProject.SelectedNode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u5220\u9664\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshSelectedNode()
    {
        var selectedNodes = GetSelectedFileSystemNodes();
        if (selectedNodes.Count == 0)
        {
            return;
        }

        foreach (var selectedNode in selectedNodes.ToList())
        {
            var selectedData = GetNodeData(selectedNode);
            if (selectedData is null)
            {
                continue;
            }

            if (selectedData.Kind == ExplorerNodeKind.Directory && selectedData.FullPath is not null)
            {
                if (!Directory.Exists(selectedData.FullPath))
                {
                    selectedNode.Remove();
                    continue;
                }

                LoadDirectoryChildren(selectedNode, selectedData.FullPath);
                selectedNode.Expand();
                continue;
            }

            if (selectedData.Kind == ExplorerNodeKind.File && selectedData.FullPath is not null)
            {
                if (!File.Exists(selectedData.FullPath))
                {
                    selectedNode.Remove();
                    continue;
                }

                selectedNode.Text = GetDisplayName(selectedData.FullPath);
            }
        }

        treeProject.Invalidate();
    }

    private void ExecuteNodeAction(TreeNode node)
    {
        var nodeData = GetNodeData(node);
        if (nodeData is null)
        {
            return;
        }

        switch (nodeData.Kind)
        {
            case ExplorerNodeKind.CommandOpenFolder:
                OpenFolderFromDialog();
                break;
            case ExplorerNodeKind.CommandOpenFile:
                OpenFilesFromDialog();
                break;
            case ExplorerNodeKind.File:
                ShowFileInEditorPlaceholder(nodeData.FullPath);
                break;
        }
    }

    private void ShowFileInEditorPlaceholder(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            OpenFileInEditorTab(filePath);
            editorControlMain.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u6253\u5F00\u6587\u4EF6\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string NormalizeEditorNewlines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private void ProjectTreeContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var cursorPoint = treeProject.PointToClient(Cursor.Position);
        if (treeProject.ClientRectangle.Contains(cursorPoint))
        {
            var hitTest = treeProject.HitTest(cursorPoint);
            if (hitTest.Node is not null)
            {
                UpdateSelectionFromNodeClick(hitTest.Node, MouseButtons.Right);
            }
        }

        var selectedNodes = GetSelectedFileSystemNodes();
        var hasSelection = selectedNodes.Count > 0;
        var singleSelection = selectedNodes.Count == 1 ? selectedNodes[0] : treeProject.SelectedNode;
        var singleData = GetNodeData(singleSelection);
        var singleKind = singleData?.Kind;
        var isSingleFile = singleKind == ExplorerNodeKind.File;
        var isSingleDirectory = singleKind == ExplorerNodeKind.Directory;
        var isSingleRootDirectory = isSingleDirectory && singleSelection?.Parent is null;
        var hasClipboardSource = GetClipboardSourcePaths().Count > 0;
        var canCreateInSelectedDirectory = isSingleDirectory && selectedNodes.Count == 1;
        var canRenameSingle = singleSelection is not null &&
            selectedNodes.Count == 1 &&
            (isSingleFile || (isSingleDirectory && !isSingleRootDirectory));
        var canDeleteAll = hasSelection &&
            selectedNodes.All(node =>
            {
                var data = GetNodeData(node);
                if (data?.Kind == ExplorerNodeKind.File)
                {
                    return true;
                }

                return data?.Kind == ExplorerNodeKind.Directory && node.Parent is not null;
            });

        contextNewFile.Enabled = canCreateInSelectedDirectory;
        contextNewFolder.Enabled = canCreateInSelectedDirectory;
        contextNewCppFile.Enabled = canCreateInSelectedDirectory;
        contextNewHppFile.Enabled = canCreateInSelectedDirectory;
        contextNewCFile.Enabled = canCreateInSelectedDirectory;
        contextNewHFile.Enabled = canCreateInSelectedDirectory;
        contextCopy.Enabled = hasSelection;
        contextPaste.Enabled = isSingleDirectory && selectedNodes.Count == 1 && hasClipboardSource;
        contextRename.Enabled = canRenameSingle;
        contextDelete.Enabled = canDeleteAll;
        contextRefresh.Enabled = hasSelection;
        contextOpenFolder.Enabled = true;
        contextOpenFile.Enabled = true;

        if (singleKind is ExplorerNodeKind.CommandOpenFile or ExplorerNodeKind.CommandOpenFolder)
        {
            contextCopy.Enabled = false;
            contextRename.Enabled = false;
            contextDelete.Enabled = false;
            contextRefresh.Enabled = false;
        }
    }

    private void TreeProject_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        var nodeData = GetNodeData(e.Node);
        if (nodeData?.Kind != ExplorerNodeKind.Directory || nodeData.FullPath is null)
        {
            return;
        }

        if (e.Node.Nodes.Count == 1 && IsLazyPlaceholderNode(e.Node.Nodes[0]))
        {
            LoadDirectoryChildren(e.Node, nodeData.FullPath);
        }
    }

    private void TreeProject_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        ExecuteNodeAction(e.Node);
    }

    private void TreeProject_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        UpdateSelectionFromNodeClick(e.Node, e.Button);
    }

    private void TreeProject_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hitTest = treeProject.HitTest(e.Location);
        if (hitTest.Node is null)
        {
            SelectSingleExplorerNode(null);
        }
    }

    private void TreeProject_BeforeLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (!IsFileSystemNode(e.Node))
        {
            e.CancelEdit = true;
            return;
        }

        if (e.Node is not null)
        {
            SelectSingleExplorerNode(e.Node);
        }

        isEditingTreeLabel = true;

        if (e.Node is null || !explorerSettings.RenameSelectNameOnly)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (treeProject.IsDisposed || !e.Node.IsEditing)
            {
                return;
            }

            var editHandle = SendMessage(treeProject.Handle, TVM_GETEDITCONTROL, 0, 0);
            if (editHandle == 0)
            {
                return;
            }

            var labelText = e.Node.Text ?? string.Empty;
            var selectEnd = labelText.Length;
            if (GetNodeData(e.Node)?.Kind == ExplorerNodeKind.File)
            {
                var extension = Path.GetExtension(labelText);
                if (!string.IsNullOrEmpty(extension))
                {
                    selectEnd = Math.Max(0, labelText.Length - extension.Length);
                }
            }

            SendMessage(editHandle, EM_SETSEL, 0, selectEnd);
        }));
    }

    private void TreeProject_AfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        isEditingTreeLabel = false;

        if (e.Node is null)
        {
            e.CancelEdit = true;
            return;
        }

        if (e.Label is null)
        {
            return;
        }

        var nodeData = GetNodeData(e.Node);
        if (nodeData?.FullPath is null)
        {
            e.CancelEdit = true;
            return;
        }

        var newName = e.Label.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            e.CancelEdit = true;
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            e.CancelEdit = true;
            MessageBox.Show(this, "\u540D\u79F0\u5305\u542B\u975E\u6CD5\u5B57\u7B26\u3002", "\u91CD\u547D\u540D", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var oldPath = nodeData.FullPath;
        var parentDirectory = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            e.CancelEdit = true;
            return;
        }

        var newPath = Path.Combine(parentDirectory, newName);
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            e.CancelEdit = true;
            MessageBox.Show(this, "\u76EE\u6807\u540D\u79F0\u5DF2\u5B58\u5728\u3002", "\u91CD\u547D\u540D", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (nodeData.Kind == ExplorerNodeKind.Directory)
            {
                Directory.Move(oldPath, newPath);
                UpdateLoadedChildPaths(e.Node, oldPath, newPath);
            }
            else if (nodeData.Kind == ExplorerNodeKind.File)
            {
                File.Move(oldPath, newPath);
            }

            nodeData.FullPath = newPath;
            e.Node.Text = GetDisplayName(newPath);
            treeProject.Invalidate();
        }
        catch (Exception ex)
        {
            e.CancelEdit = true;
            MessageBox.Show(this, ex.Message, "\u91CD\u547D\u540D\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void UpdateLoadedChildPaths(TreeNode parentNode, string oldBasePath, string newBasePath)
    {
        foreach (TreeNode childNode in parentNode.Nodes)
        {
            var childData = GetNodeData(childNode);
            if (childData?.FullPath is not null &&
                childData.FullPath.StartsWith(oldBasePath, StringComparison.OrdinalIgnoreCase))
            {
                childData.FullPath = newBasePath + childData.FullPath.Substring(oldBasePath.Length);
            }

            UpdateLoadedChildPaths(childNode, oldBasePath, newBasePath);
        }
    }

    private void TreeProject_KeyDown(object? sender, KeyEventArgs e)
    {
        if (isEditingTreeLabel)
        {
            return;
        }

        if (e.Control && e.KeyCode == Keys.A)
        {
            selectedExplorerNodes.Clear();
            foreach (var node in EnumerateVisibleNodes())
            {
                if (IsFileSystemNode(node))
                {
                    selectedExplorerNodes.Add(node);
                }
            }

            if (selectedExplorerNodes.Count > 0)
            {
                treeProject.SelectedNode = selectedExplorerNodes[^1];
                explorerSelectionAnchorNode = selectedExplorerNodes[0];
            }

            treeProject.Invalidate();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.FileOpenFolder))
        {
            OpenFolderFromDialog();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerNewFolder))
        {
            CreateFolder();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.FileOpen))
        {
            OpenFilesFromDialog();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerNewFile))
        {
            CreateGeneralFile();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerCopy))
        {
            CopySelectedNode();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerPaste))
        {
            PasteIntoSelectedLocation();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerRename))
        {
            BeginRenameSelectedNode();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerDelete))
        {
            DeleteSelectedNode();
            ConsumeKey(e);
            return;
        }

        if (IsShortcutTriggered(e, EditorCommandIds.ExplorerRefresh))
        {
            RefreshSelectedNode();
            ConsumeKey(e);
            return;
        }

        if (e.KeyCode == Keys.Enter && treeProject.SelectedNode is not null)
        {
            ExecuteNodeAction(treeProject.SelectedNode);
            ConsumeKey(e);
        }
    }
}

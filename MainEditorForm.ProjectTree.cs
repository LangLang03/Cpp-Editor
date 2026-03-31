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

    private string? copiedNodePath;
    private bool isEditingTreeLabel;

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

        projectTreeContextMenu = CreateProjectTreeContextMenu();
        projectTree.ContextMenuStrip = projectTreeContextMenu;

        ResetTreeToQuickOpen(projectTree);
        return projectTree;
    }

    private void ResetTreeToQuickOpen(TreeView projectTree)
    {
        projectTree.Nodes.Clear();
        projectTree.Nodes.Add(CreateCommandNode("\u6253\u5F00\u6587\u4EF6\u5939...", ExplorerNodeKind.CommandOpenFolder));
        projectTree.Nodes.Add(CreateCommandNode("\u6253\u5F00\u6587\u4EF6...", ExplorerNodeKind.CommandOpenFile));
    }

    private ContextMenuStrip CreateProjectTreeContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        contextOpenFolder = new ToolStripMenuItem("\u6253\u5F00\u6587\u4EF6\u5939...", null, (_, _) => OpenFolderFromDialog())
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+O"
        };
        contextOpenFile = new ToolStripMenuItem("\u6253\u5F00\u6587\u4EF6...", null, (_, _) => OpenFilesFromDialog())
        {
            ShortcutKeyDisplayString = "Ctrl+O"
        };
        contextNewFile = new ToolStripMenuItem("\u65B0\u5EFA\u6587\u4EF6", null, (_, _) => CreateGeneralFile())
        {
            ShortcutKeyDisplayString = "Ctrl+N"
        };
        contextNewFolder = new ToolStripMenuItem("\u65B0\u5EFA\u6587\u4EF6\u5939", null, (_, _) => CreateFolder())
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+N"
        };
        contextNewCppFile = new ToolStripMenuItem("\u65B0\u5EFA C++ \u6E90\u6587\u4EF6 (.cpp)", null, (_, _) => CreateCppFile());
        contextNewHppFile = new ToolStripMenuItem("\u65B0\u5EFA C++ \u5934\u6587\u4EF6 (.hpp)", null, (_, _) => CreateHppFile());
        contextNewCFile = new ToolStripMenuItem("\u65B0\u5EFA C \u6E90\u6587\u4EF6 (.c)", null, (_, _) => CreateCFile());
        contextNewHFile = new ToolStripMenuItem("\u65B0\u5EFA C \u5934\u6587\u4EF6 (.h)", null, (_, _) => CreateHFile());
        contextCopy = new ToolStripMenuItem("\u590D\u5236", null, (_, _) => CopySelectedNode())
        {
            ShortcutKeyDisplayString = "Ctrl+C"
        };
        contextPaste = new ToolStripMenuItem("\u7C98\u8D34", null, (_, _) => PasteIntoSelectedLocation())
        {
            ShortcutKeyDisplayString = "Ctrl+V"
        };
        contextRename = new ToolStripMenuItem("\u91CD\u547D\u540D", null, (_, _) => BeginRenameSelectedNode())
        {
            ShortcutKeyDisplayString = "F2"
        };
        contextDelete = new ToolStripMenuItem("\u5220\u9664", null, (_, _) => DeleteSelectedNode())
        {
            ShortcutKeyDisplayString = "Del"
        };
        contextRefresh = new ToolStripMenuItem("\u5237\u65B0", null, (_, _) => RefreshSelectedNode())
        {
            ShortcutKeyDisplayString = "F5"
        };

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
        return contextMenu;
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

        string? firstOpenedFile = null;
        foreach (var filePath in dialog.FileNames.Where(File.Exists))
        {
            AddOpenedFileNode(filePath, beginEdit: false);
            firstOpenedFile ??= filePath;
        }

        if (!string.IsNullOrWhiteSpace(firstOpenedFile))
        {
            ShowFileInEditorPlaceholder(firstOpenedFile);
        }
    }

    private void AddOpenedFolderNode(string folderPath, bool beginEdit)
    {
        var existing = FindNodeByPath(folderPath);
        if (existing is not null)
        {
            treeProject.SelectedNode = existing;
            existing.EnsureVisible();
            return;
        }

        var folderNode = CreateDirectoryNode(folderPath);
        treeProject.Nodes.Add(folderNode);
        treeProject.SelectedNode = folderNode;
        folderNode.Expand();

        if (beginEdit)
        {
            folderNode.BeginEdit();
        }
    }

    private void AddOpenedFileNode(string filePath, bool beginEdit)
    {
        var existing = FindNodeByPath(filePath);
        if (existing is not null)
        {
            treeProject.SelectedNode = existing;
            existing.EnsureVisible();
            return;
        }

        var fileNode = CreateFileNode(filePath);
        treeProject.Nodes.Add(fileNode);
        treeProject.SelectedNode = fileNode;
        fileNode.EnsureVisible();

        if (beginEdit)
        {
            fileNode.BeginEdit();
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

    private string? GetClipboardSourcePath()
    {
        if (!string.IsNullOrWhiteSpace(copiedNodePath) &&
            (File.Exists(copiedNodePath) || Directory.Exists(copiedNodePath)))
        {
            return copiedNodePath;
        }

        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var fileDropList = Clipboard.GetFileDropList();
                if (fileDropList.Count > 0)
                {
                    return fileDropList[0];
                }
            }
        }
        catch
        {
            // Ignore clipboard access failures and fall back to internal clipboard.
        }

        return null;
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
            using (File.Create(createdPath))
            {
            }

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
            treeProject.SelectedNode = existingNode;
            existingNode.EnsureVisible();
            if (beginEdit)
            {
                existingNode.BeginEdit();
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
                    treeProject.SelectedNode = existingNode;
                    existingNode.EnsureVisible();
                    if (beginEdit)
                    {
                        existingNode.BeginEdit();
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
        var selectedData = GetNodeData(treeProject.SelectedNode);
        if (selectedData?.FullPath is null)
        {
            return;
        }

        copiedNodePath = selectedData.FullPath;

        try
        {
            var fileDropList = new System.Collections.Specialized.StringCollection();
            fileDropList.Add(copiedNodePath);
            Clipboard.SetFileDropList(fileDropList);
        }
        catch
        {
            // Internal clipboard still works even if system clipboard is unavailable.
        }
    }

    private void PasteIntoSelectedLocation()
    {
        var sourcePath = GetClipboardSourcePath();
        if (string.IsNullOrWhiteSpace(sourcePath))
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
            if (Directory.Exists(sourcePath))
            {
                if (IsInsideDirectory(targetDirectory, sourcePath))
                {
                    MessageBox.Show(this, "\u4E0D\u80FD\u5C06\u6587\u4EF6\u5939\u7C98\u8D34\u5230\u5176\u81EA\u8EAB\u6216\u5B50\u76EE\u5F55\u4E2D\u3002", "\u7C98\u8D34\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var targetPath = BuildUniquePath(targetDirectory, Path.GetFileName(sourcePath));
                CopyDirectory(sourcePath, targetPath);
                RevealPath(targetPath, beginEdit: false);
                return;
            }

            if (File.Exists(sourcePath))
            {
                var targetPath = BuildUniquePath(targetDirectory, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, targetPath);
                RevealPath(targetPath, beginEdit: false);
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

    private void BeginRenameSelectedNode()
    {
        if (!IsFileSystemNode(treeProject.SelectedNode))
        {
            return;
        }

        treeProject.SelectedNode?.BeginEdit();
    }

    private void DeleteSelectedNode()
    {
        var selectedNode = treeProject.SelectedNode;
        var selectedData = GetNodeData(selectedNode);
        if (selectedData?.FullPath is null)
        {
            return;
        }

        var nodeTypeText = selectedData.Kind == ExplorerNodeKind.Directory
            ? "\u6587\u4EF6\u5939"
            : "\u6587\u4EF6";
        var result = MessageBox.Show(
            this,
            $"\u786E\u5B9A\u5220\u9664{nodeTypeText} \"{selectedNode!.Text}\" \u5417\uFF1F",
            "\u786E\u8BA4\u5220\u9664",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (selectedData.Kind == ExplorerNodeKind.Directory && Directory.Exists(selectedData.FullPath))
            {
                Directory.Delete(selectedData.FullPath, true);
            }
            else if (selectedData.Kind == ExplorerNodeKind.File && File.Exists(selectedData.FullPath))
            {
                File.Delete(selectedData.FullPath);
            }

            selectedNode.Remove();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u5220\u9664\u5931\u8D25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshSelectedNode()
    {
        var selectedNode = treeProject.SelectedNode;
        var selectedData = GetNodeData(selectedNode);
        if (selectedNode is null || selectedData is null)
        {
            return;
        }

        if (selectedData.Kind == ExplorerNodeKind.Directory && selectedData.FullPath is not null)
        {
            if (!Directory.Exists(selectedData.FullPath))
            {
                selectedNode.Remove();
                return;
            }

            LoadDirectoryChildren(selectedNode, selectedData.FullPath);
            selectedNode.Expand();
            return;
        }

        if (selectedData.Kind == ExplorerNodeKind.File && selectedData.FullPath is not null)
        {
            if (!File.Exists(selectedData.FullPath))
            {
                selectedNode.Remove();
                return;
            }

            selectedNode.Text = GetDisplayName(selectedData.FullPath);
        }
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
        if (string.IsNullOrWhiteSpace(filePath) || tabEditorHost.TabPages.Count == 0)
        {
            return;
        }

        var firstTab = tabEditorHost.TabPages[0];
        firstTab.Text = GetDisplayName(filePath);
        firstTab.ToolTipText = filePath;

        if (editorControlMain is null)
        {
            return;
        }

        try
        {
            var fileContent = File.ReadAllText(filePath);
            var normalized = NormalizeEditorNewlines(fileContent);
            SetEditorSyntaxSource(filePath, normalized);
            editorControlMain.LoadDocument(new SweetEditor.Document(normalized));
            editorControlMain.RequestDecorationRefresh();
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
                treeProject.SelectedNode = hitTest.Node;
            }
        }

        var selectedNode = treeProject.SelectedNode;
        var selectedData = GetNodeData(selectedNode);
        var selectedKind = selectedData?.Kind;
        var isFile = selectedKind == ExplorerNodeKind.File;
        var isDirectory = selectedKind == ExplorerNodeKind.Directory;
        var isRootDirectory = isDirectory && selectedNode?.Parent is null;
        var hasClipboardSource = !string.IsNullOrWhiteSpace(GetClipboardSourcePath());
        var canCreateInSelectedDirectory = isDirectory;

        contextNewFile.Enabled = canCreateInSelectedDirectory;
        contextNewFolder.Enabled = canCreateInSelectedDirectory;
        contextNewCppFile.Enabled = canCreateInSelectedDirectory;
        contextNewHppFile.Enabled = canCreateInSelectedDirectory;
        contextNewCFile.Enabled = canCreateInSelectedDirectory;
        contextNewHFile.Enabled = canCreateInSelectedDirectory;
        contextCopy.Enabled = isFile || isDirectory;
        contextPaste.Enabled = isDirectory && hasClipboardSource;
        contextRename.Enabled = isFile || (isDirectory && !isRootDirectory);
        contextDelete.Enabled = isFile || (isDirectory && !isRootDirectory);
        contextRefresh.Enabled = isFile || isDirectory;
        contextOpenFolder.Enabled = true;
        contextOpenFile.Enabled = true;

        if (selectedKind is ExplorerNodeKind.CommandOpenFile or ExplorerNodeKind.CommandOpenFolder)
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
        if (e.Button == MouseButtons.Right)
        {
            treeProject.SelectedNode = e.Node;
        }
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
            treeProject.SelectedNode = null;
        }
    }

    private void TreeProject_BeforeLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (!IsFileSystemNode(e.Node))
        {
            e.CancelEdit = true;
            return;
        }

        isEditingTreeLabel = true;
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

        if (e.Control && e.Shift && e.KeyCode == Keys.O)
        {
            OpenFolderFromDialog();
            ConsumeKey(e);
            return;
        }

        if (e.Control && e.Shift && e.KeyCode == Keys.N)
        {
            CreateFolder();
            ConsumeKey(e);
            return;
        }

        if (e.Control && e.KeyCode == Keys.O)
        {
            OpenFilesFromDialog();
            ConsumeKey(e);
            return;
        }

        if (e.Control && e.KeyCode == Keys.N)
        {
            CreateGeneralFile();
            ConsumeKey(e);
            return;
        }

        if (e.Control && e.KeyCode == Keys.C)
        {
            CopySelectedNode();
            ConsumeKey(e);
            return;
        }

        if (e.Control && e.KeyCode == Keys.V)
        {
            PasteIntoSelectedLocation();
            ConsumeKey(e);
            return;
        }

        if (e.KeyCode == Keys.F2)
        {
            BeginRenameSelectedNode();
            ConsumeKey(e);
            return;
        }

        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedNode();
            ConsumeKey(e);
            return;
        }

        if (e.KeyCode == Keys.F5)
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

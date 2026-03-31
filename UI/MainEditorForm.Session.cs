namespace C__Editor;

public partial class MainEditorForm
{
    private void RestoreLastSessionOnStartupIfNeeded()
    {
        if (!uiSettings.RestoreLastSessionOnStartup)
        {
            return;
        }

        if (!IsNormalDoubleClickLaunch())
        {
            return;
        }

        var session = EditorSessionStateController.Load();
        var hasExplorerState = session.OpenedFolderPaths.Count > 0 || session.OpenedFilePaths.Count > 0;
        var hasEditorState = session.OpenDocumentPaths.Count > 0;
        if (!hasExplorerState && !hasEditorState)
        {
            return;
        }

        foreach (var folderPath in session.OpenedFolderPaths)
        {
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            AddOpenedFolderNode(folderPath, beginEdit: false);
        }

        foreach (var filePath in session.OpenedFilePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            AddOpenedFileNode(filePath, beginEdit: false);
        }

        var restoredAnyDocument = false;
        foreach (var filePath in session.OpenDocumentPaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            try
            {
                if (!restoredAnyDocument)
                {
                    CloseDefaultStartupTabIfNeeded();
                    restoredAnyDocument = true;
                }

                OpenFileInEditorTab(filePath);
            }
            catch (Exception ex)
            {
                AppendBuildOutput($"恢复会话文件失败: {filePath} ({ex.Message})");
            }
        }

        if (!string.IsNullOrWhiteSpace(session.ActiveDocumentPath))
        {
            var activeTab = FindTabByFilePath(session.ActiveDocumentPath);
            if (activeTab is not null)
            {
                tabEditorHost.SelectedTab = activeTab;
                ActivateDocumentTab(activeTab);
                if (editorControlMain is not null)
                {
                    editorControlMain.GotoPosition(session.ActiveCursorLine, session.ActiveCursorColumn);
                    editorControlMain.Focus();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(session.SelectedExplorerPath))
        {
            var selectedNode = FindNodeByPath(session.SelectedExplorerPath);
            if (selectedNode is not null)
            {
                treeProject.SelectedNode = selectedNode;
                selectedNode.EnsureVisible();
            }
        }

        AppendBuildOutput("已恢复上次会话。");
    }

    private static bool IsNormalDoubleClickLaunch()
    {
        var args = Environment.GetCommandLineArgs();
        return args.Length <= 1;
    }

    private void PersistSessionStateOnExit()
    {
        if (!uiSettings.RestoreLastSessionOnStartup)
        {
            EditorSessionStateController.Clear();
            return;
        }

        var session = CaptureSessionState();
        EditorSessionStateController.Save(session);
    }

    private EditorSessionState CaptureSessionState()
    {
        SyncActiveDocumentSnapshot();

        var session = new EditorSessionState();

        if (treeProject is not null)
        {
            foreach (TreeNode rootNode in treeProject.Nodes)
            {
                var nodeData = GetNodeData(rootNode);
                if (nodeData?.Kind == ExplorerNodeKind.Directory &&
                    !string.IsNullOrWhiteSpace(nodeData.FullPath))
                {
                    session.OpenedFolderPaths.Add(nodeData.FullPath);
                }
                else if (nodeData?.Kind == ExplorerNodeKind.File &&
                    !string.IsNullOrWhiteSpace(nodeData.FullPath))
                {
                    session.OpenedFilePaths.Add(nodeData.FullPath);
                }
            }

            var selectedNodeData = GetNodeData(treeProject.SelectedNode);
            if (!string.IsNullOrWhiteSpace(selectedNodeData?.FullPath))
            {
                session.SelectedExplorerPath = selectedNodeData.FullPath;
            }
        }

        if (tabEditorHost is not null)
        {
            foreach (TabPage tab in tabEditorHost.TabPages)
            {
                var state = GetDocumentState(tab);
                if (state is null || string.IsNullOrWhiteSpace(state.FilePath))
                {
                    continue;
                }

                session.OpenDocumentPaths.Add(state.FilePath);
            }

            var selectedState = GetSelectedDocumentState();
            if (!string.IsNullOrWhiteSpace(selectedState?.FilePath))
            {
                session.ActiveDocumentPath = selectedState.FilePath;
            }
        }

        if (editorControlMain is not null)
        {
            try
            {
                var cursor = editorControlMain.GetCursorPosition();
                session.ActiveCursorLine = Math.Max(0, cursor.Line);
                session.ActiveCursorColumn = Math.Max(0, cursor.Column);
            }
            catch
            {
                session.ActiveCursorLine = 0;
                session.ActiveCursorColumn = 0;
            }
        }

        return session;
    }

    private void CloseDefaultStartupTabIfNeeded()
    {
        if (tabEditorHost is null || tabEditorHost.TabPages.Count != 1)
        {
            return;
        }

        var onlyTab = tabEditorHost.TabPages[0];
        var state = GetDocumentState(onlyTab);
        if (state is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.FilePath) || state.IsDirty)
        {
            return;
        }

        var normalizedText = NormalizeEditorNewlines(state.TextContent).Trim();
        if (!string.Equals(normalizedText, "// Ready", StringComparison.Ordinal))
        {
            return;
        }

        if (ReferenceEquals(editorControlMain?.Parent, onlyTab))
        {
            onlyTab.Controls.Remove(editorControlMain);
        }

        editorDocuments.Remove(onlyTab);
        tabEditorHost.TabPages.Remove(onlyTab);
        onlyTab.Dispose();
        activeEditorTab = null;
        currentEditorFilePath = null;
        hasUnsavedChanges = false;
    }
}

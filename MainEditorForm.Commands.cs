using System.Text;

namespace C__Editor;

public partial class MainEditorForm
{
    private string? currentEditorFilePath;
    private bool hasUnsavedChanges;
    private bool isLoadingEditorDocument;
    private string lastFindText = string.Empty;

    private void AttachEditorEventHandlers()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.TextChanged -= EditorControlMain_TextChanged;
        editorControlMain.TextChanged += EditorControlMain_TextChanged;
    }

    private void EditorControlMain_TextChanged(object? sender, SweetEditor.TextChangedEventArgs e)
    {
        if (isLoadingEditorDocument)
        {
            return;
        }

        hasUnsavedChanges = true;
        var state = GetSelectedDocumentState();
        if (state is not null)
        {
            state.IsDirty = true;
        }

        UpdateEditorTabHeader(targetTab: tabEditorHost.SelectedTab);
    }

    private void NewUntitledDocument()
    {
        var tab = CreateDocumentTab("// New File\n", null, BuildUntitledName(), markClean: true);
        tabEditorHost.SelectedTab = tab;
        ActivateDocumentTab(tab);
        editorControlMain?.Focus();
    }

    private void LoadEditorDocumentText(string text, string? filePath, string? displayName = null, bool markClean = true)
    {
        if (tabEditorHost is null)
        {
            return;
        }

        if (tabEditorHost.SelectedTab is null)
        {
            var initialTab = CreateDocumentTab(string.Empty, null, BuildUntitledName(), markClean: true);
            tabEditorHost.SelectedTab = initialTab;
        }

        var selectedTab = tabEditorHost.SelectedTab;
        var state = GetDocumentState(selectedTab);
        if (selectedTab is null || state is null)
        {
            return;
        }

        var normalized = NormalizeEditorNewlines(text);
        state.FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        state.DisplayName = ResolveDocumentDisplayName(state.FilePath, displayName);
        state.TextContent = normalized;
        state.IsDirty = !markClean;

        if (activeEditorTab != selectedTab)
        {
            ActivateDocumentTab(selectedTab);
        }

        if (editorControlMain is not null)
        {
            isLoadingEditorDocument = true;
            try
            {
                var syntaxSource = state.FilePath ?? state.DisplayName;
                ApplyEditorLanguageConfiguration(syntaxSource);
                SetEditorSyntaxSource(syntaxSource, normalized);
                editorControlMain.LoadDocument(new SweetEditor.Document(normalized));
                editorControlMain.RequestDecorationRefresh();
            }
            finally
            {
                isLoadingEditorDocument = false;
            }
        }

        currentEditorFilePath = state.FilePath;
        hasUnsavedChanges = state.IsDirty;
        UpdateEditorTabHeader(targetTab: selectedTab);
    }

    private void UpdateEditorTabHeader(string? displayName = null, TabPage? targetTab = null)
    {
        if (tabEditorHost is null || tabEditorHost.TabPages.Count == 0)
        {
            return;
        }

        var tabPage = targetTab ?? tabEditorHost.SelectedTab;
        if (tabPage is null)
        {
            return;
        }

        var state = GetDocumentState(tabPage);
        if (state is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            state.DisplayName = displayName;
        }

        var baseName = ResolveDocumentDisplayName(state.FilePath, state.DisplayName);
        tabPage.Text = state.IsDirty ? $"*{baseName}" : baseName;
        tabPage.ToolTipText = state.FilePath ?? string.Empty;

        if (tabPage == tabEditorHost.SelectedTab)
        {
            currentEditorFilePath = state.FilePath;
            hasUnsavedChanges = state.IsDirty;
        }
    }

    private bool SaveCurrentDocument()
    {
        if (editorControlMain is null)
        {
            return false;
        }

        var selectedTab = tabEditorHost.SelectedTab;
        var state = GetDocumentState(selectedTab);
        if (selectedTab is null || state is null)
        {
            return false;
        }

        SyncActiveDocumentSnapshot();
        if (string.IsNullOrWhiteSpace(state.FilePath))
        {
            return SaveCurrentDocumentAs();
        }

        try
        {
            File.WriteAllText(state.FilePath, state.TextContent, new UTF8Encoding(false));
            state.IsDirty = false;
            currentEditorFilePath = state.FilePath;
            hasUnsavedChanges = false;
            UpdateEditorTabHeader(targetTab: selectedTab);
            AddOpenedFileNode(state.FilePath, beginEdit: false);
            AppendBuildOutput($"已保存: {state.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool SaveCurrentDocumentAs()
    {
        if (editorControlMain is null)
        {
            return false;
        }

        var selectedTab = tabEditorHost.SelectedTab;
        var state = GetDocumentState(selectedTab);
        if (selectedTab is null || state is null)
        {
            return false;
        }

        SyncActiveDocumentSnapshot();

        using var dialog = new SaveFileDialog
        {
            Title = "另存为",
            Filter = "C/C++ Files (*.cpp;*.c;*.h;*.hpp)|*.cpp;*.c;*.h;*.hpp|All Files (*.*)|*.*",
            FileName = !string.IsNullOrWhiteSpace(state.FilePath)
                ? Path.GetFileName(state.FilePath)
                : ResolveDocumentDisplayName(null, state.DisplayName),
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        try
        {
            var normalizedPath = Path.GetFullPath(dialog.FileName);
            File.WriteAllText(normalizedPath, state.TextContent, new UTF8Encoding(false));

            state.FilePath = normalizedPath;
            state.DisplayName = Path.GetFileName(normalizedPath);
            state.IsDirty = false;
            currentEditorFilePath = normalizedPath;
            hasUnsavedChanges = false;

            UpdateEditorTabHeader(targetTab: selectedTab);
            AddOpenedFileNode(normalizedPath, beginEdit: false);
            AppendBuildOutput($"已另存为: {normalizedPath}");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "另存为失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void CloseCurrentDocument()
    {
        if (tabEditorHost?.SelectedTab is null)
        {
            return;
        }

        TryCloseEditorTab(tabEditorHost.SelectedTab, createFallbackIfEmpty: true);
    }

    private bool EnsureCanDiscardUnsavedChanges()
    {
        if (tabEditorHost?.SelectedTab is null)
        {
            return true;
        }

        return ConfirmCloseForTab(tabEditorHost.SelectedTab);
    }

    private bool EnsureCanSwitchEditorTarget(string? targetPath)
    {
        return true;
    }

    private string GetEditorText()
    {
        if (editorControlMain is null)
        {
            return string.Empty;
        }

        var document = editorControlMain.GetDocument();
        if (document is null)
        {
            return string.Empty;
        }

        var lineCount = document.GetLineCount();
        if (lineCount <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < lineCount; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(document.GetLineText(i));
        }

        return builder.ToString();
    }

    private void UndoInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.Undo();
        editorControlMain.Focus();
    }

    private void RedoInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.Redo();
        editorControlMain.Focus();
    }

    private void CutInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var selectedText = editorControlMain.GetSelectedText();
        if (!string.IsNullOrEmpty(selectedText))
        {
            Clipboard.SetText(selectedText);
        }

        var selection = editorControlMain.GetSelection();
        if (selection.hasSelection)
        {
            editorControlMain.DeleteText(selection.range);
        }

        editorControlMain.Focus();
    }

    private void CopyInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var selectedText = editorControlMain.GetSelectedText();
        if (!string.IsNullOrEmpty(selectedText))
        {
            Clipboard.SetText(selectedText);
        }

        editorControlMain.Focus();
    }

    private void PasteInEditor()
    {
        if (editorControlMain is null || !Clipboard.ContainsText())
        {
            return;
        }

        editorControlMain.InsertText(Clipboard.GetText());
        editorControlMain.Focus();
    }

    private void SelectAllInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.SelectAll();
        editorControlMain.Focus();
    }

    private void FindInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var input = TextInputDialog.Show(this, "查找", "请输入要查找的文本:", lastFindText);
        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        lastFindText = input;
        if (!FindNextOccurrence(input))
        {
            MessageBox.Show(this, $"未找到 \"{input}\"", "查找", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private bool FindNextOccurrence(string keyword)
    {
        if (editorControlMain is null || string.IsNullOrEmpty(keyword))
        {
            return false;
        }

        var text = GetEditorText();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var cursor = editorControlMain.GetCursorPosition();
        var startOffset = LineColumnToOffset(text, cursor.Line, cursor.Column);
        if (startOffset < 0 || startOffset > text.Length)
        {
            startOffset = 0;
        }

        var index = text.IndexOf(keyword, startOffset, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && startOffset > 0)
        {
            index = text.IndexOf(keyword, 0, startOffset, StringComparison.OrdinalIgnoreCase);
        }

        if (index < 0)
        {
            return false;
        }

        var start = OffsetToTextPosition(text, index);
        var end = OffsetToTextPosition(text, index + keyword.Length);
        editorControlMain.SetSelection(start.Line, start.Column, end.Line, end.Column);
        editorControlMain.ScrollToLine(start.Line, SweetEditor.ScrollBehavior.CENTER);
        editorControlMain.Focus();
        return true;
    }

    private void ReplaceInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var findText = TextInputDialog.Show(this, "替换", "查找内容:", lastFindText);
        if (string.IsNullOrEmpty(findText))
        {
            return;
        }

        var replaceText = TextInputDialog.Show(this, "替换", "替换为:", string.Empty);
        if (replaceText is null)
        {
            return;
        }

        lastFindText = findText;
        var selection = editorControlMain.GetSelection();
        var selectedText = editorControlMain.GetSelectedText();
        if (!selection.hasSelection || !string.Equals(selectedText, findText, StringComparison.OrdinalIgnoreCase))
        {
            if (!FindNextOccurrence(findText))
            {
                MessageBox.Show(this, $"未找到 \"{findText}\"", "替换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            selection = editorControlMain.GetSelection();
        }

        editorControlMain.ReplaceText(selection.range, replaceText);
        FindNextOccurrence(findText);
        editorControlMain.Focus();
    }

    private void GoToLineInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var lineInput = TextInputDialog.Show(this, "转到行", "请输入行号(从 1 开始):", string.Empty);
        if (string.IsNullOrWhiteSpace(lineInput))
        {
            return;
        }

        if (!int.TryParse(lineInput, out var lineNumber) || lineNumber < 1)
        {
            MessageBox.Show(this, "请输入有效的正整数行号。", "转到行", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        editorControlMain.GotoPosition(lineNumber - 1, 0);
        editorControlMain.Focus();
    }

    private void ToggleProjectTreePanel(bool visible)
    {
        splitWorkspace.Panel1Collapsed = !visible;
        if (!splitWorkspace.Panel1Collapsed && splitWorkspace.SplitterDistance <= 0)
        {
            splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        }

        if (!suppressViewMenuStateSync)
        {
            PersistUiSettingsFromCurrentState();
        }
    }

    private void ToggleOutputPanel(bool visible)
    {
        splitMain.Panel2Collapsed = !visible;
        if (!suppressViewMenuStateSync)
        {
            PersistUiSettingsFromCurrentState();
        }
    }

    private void ResetMainLayout()
    {
        splitWorkspace.Panel1Collapsed = false;
        splitMain.Panel2Collapsed = false;

        splitMain.SplitterDistance = Math.Max(260, (int)(ClientSize.Height * 0.68));
        splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        SyncViewMenuState();
        PersistUiSettingsFromCurrentState();
    }

    private void ExecuteBuildCommand(string commandName)
    {
        tabBottom.SelectedIndex = 0;
        rtbBuildOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {commandName} (占位)\r\n");

        if (string.Equals(commandName, "运行", StringComparison.Ordinal))
        {
            tabBottom.SelectedIndex = 2;
            rtbRunOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] 暂未接入运行器。\r\n");
        }
    }

    private void ExecuteDebugCommand(string commandName)
    {
        tabBottom.SelectedIndex = 0;
        rtbBuildOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {commandName} (占位)\r\n");
    }

    private void AppendBuildOutput(string message)
    {
        if (rtbBuildOutput is null)
        {
            return;
        }

        rtbBuildOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }

    private void ShowNotImplemented(string featureName)
    {
        MessageBox.Show(this, $"{featureName} 将在后续版本实现。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowUsageGuide()
    {
        var guideText =
            "常用快捷键:\r\n" +
            $"新建文件: {GetShortcutHint(EditorCommandIds.FileNew)}\r\n" +
            $"打开文件: {GetShortcutHint(EditorCommandIds.FileOpen)}\r\n" +
            $"打开文件夹: {GetShortcutHint(EditorCommandIds.FileOpenFolder)}\r\n" +
            $"保存: {GetShortcutHint(EditorCommandIds.FileSave)}\r\n" +
            $"查找: {GetShortcutHint(EditorCommandIds.EditFind)}\r\n" +
            $"替换: {GetShortcutHint(EditorCommandIds.EditReplace)}\r\n" +
            $"转到行: {GetShortcutHint(EditorCommandIds.EditGoToLine)}\r\n" +
            $"编译: {GetShortcutHint(EditorCommandIds.BuildCompile)}\r\n" +
            $"调试: {GetShortcutHint(EditorCommandIds.DebugStart)}\r\n" +
            $"运行: {GetShortcutHint(EditorCommandIds.BuildRun)}\r\n" +
            $"设置: {GetShortcutHint(EditorCommandIds.ViewOpenSettings)}";

        MessageBox.Show(this, guideText, "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAboutDialog()
    {
        MessageBox.Show(this, "C++Editor\nWinForms 代码编辑器原型", "关于 C++Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private string GetShortcutHint(string commandId)
    {
        var gesture = GetShortcutDisplayText(commandId);
        return string.IsNullOrWhiteSpace(gesture) ? "(未绑定)" : gesture;
    }

    private static int LineColumnToOffset(string text, int line, int column)
    {
        var currentLine = 0;
        var index = 0;
        while (index < text.Length && currentLine < line)
        {
            if (text[index++] == '\n')
            {
                currentLine++;
            }
        }

        var currentColumn = 0;
        while (index < text.Length && currentColumn < column && text[index] != '\n')
        {
            index++;
            currentColumn++;
        }

        return index;
    }

    private static SweetEditor.TextPosition OffsetToTextPosition(string text, int offset)
    {
        var safeOffset = Math.Clamp(offset, 0, text.Length);
        var line = 0;
        var column = 0;
        for (var i = 0; i < safeOffset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 0;
            }
            else
            {
                column++;
            }
        }

        return new SweetEditor.TextPosition
        {
            Line = line,
            Column = column
        };
    }
}

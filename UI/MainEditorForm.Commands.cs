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
        editorControlMain.CursorChanged -= EditorControlMain_CursorChanged;
        editorControlMain.SelectionChanged -= EditorControlMain_SelectionChanged;
        editorControlMain.ContextMenu -= EditorControlMain_ContextMenu;
        editorControlMain.MouseDown -= EditorControlMain_MouseDown;
        editorControlMain.TextChanged += EditorControlMain_TextChanged;
        editorControlMain.CursorChanged += EditorControlMain_CursorChanged;
        editorControlMain.SelectionChanged += EditorControlMain_SelectionChanged;
        editorControlMain.ContextMenu += EditorControlMain_ContextMenu;
        editorControlMain.MouseDown += EditorControlMain_MouseDown;
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

        InvalidateCodeStructureCacheForCurrentFile();
        UpdateEditorTabHeader(targetTab: tabEditorHost.SelectedTab);
    }

    private void EditorControlMain_CursorChanged(object? sender, SweetEditor.CursorChangedEventArgs e)
    {
        UpdateEditorStatusBar(e.CursorPosition);
    }

    private void EditorControlMain_SelectionChanged(object? sender, SweetEditor.SelectionChangedEventArgs e)
    {
        UpdateEditorStatusBar(e.CursorPosition);
    }

    private void EditorControlMain_ContextMenu(object? sender, SweetEditor.ContextMenuEventArgs e)
    {
        ShowEditorContextMenu(new PointF(e.ScreenPoint.X, e.ScreenPoint.Y));
    }

    private void NewUntitledDocument()
    {
        var tab = CreateDocumentTab("// New File\n", null, BuildUntitledName(), markClean: true);
        tabEditorHost.SelectedTab = tab;
        ActivateDocumentTab(tab);
        editorControlMain?.Focus();
    }

    private void LoadEditorDocumentText(
        string text,
        string? filePath,
        string? displayName = null,
        bool markClean = true,
        Encoding? textEncoding = null,
        string? encodingDisplayName = null)
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
        state.TextEncoding = textEncoding ?? state.TextEncoding;
        state.EncodingDisplayName = string.IsNullOrWhiteSpace(encodingDisplayName)
            ? EditorFileEncodingHelper.GetDisplayName(state.TextEncoding)
            : encodingDisplayName!;

        if (activeEditorTab != selectedTab)
        {
            ActivateDocumentTab(selectedTab);
        }

        currentEditorFilePath = state.FilePath;
        hasUnsavedChanges = state.IsDirty;

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
                ApplyBreakpointMarkersForCurrentDocument();
            }
            finally
            {
                isLoadingEditorDocument = false;
            }
        }

        UpdateEditorTabHeader(targetTab: selectedTab);
        UpdateEditorStatusBar();
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
            UpdateEditorStatusBar();
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
            var utf8Encoding = new UTF8Encoding(false);
            File.WriteAllText(state.FilePath, state.TextContent, utf8Encoding);
            state.TextEncoding = utf8Encoding;
            state.EncodingDisplayName = "UTF-8";
            state.IsDirty = false;
            currentEditorFilePath = state.FilePath;
            hasUnsavedChanges = false;
            InvalidateCodeStructureCacheForPath(state.FilePath);
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

        var previousPath = state.FilePath;
        try
        {
            var normalizedPath = Path.GetFullPath(dialog.FileName);
            var utf8Encoding = new UTF8Encoding(false);
            File.WriteAllText(normalizedPath, state.TextContent, utf8Encoding);

            state.FilePath = normalizedPath;
            state.DisplayName = Path.GetFileName(normalizedPath);
            state.TextEncoding = utf8Encoding;
            state.EncodingDisplayName = "UTF-8";
            state.IsDirty = false;
            currentEditorFilePath = normalizedPath;
            hasUnsavedChanges = false;
            ApplyBreakpointMarkersForCurrentDocument();
            InvalidateCodeStructureCacheForPath(previousPath);
            InvalidateCodeStructureCacheForPath(normalizedPath);

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

    private void ReopenCurrentDocumentWithEncoding(Encoding encoding, string displayName)
    {
        var selectedTab = tabEditorHost?.SelectedTab;
        var state = GetDocumentState(selectedTab);
        if (selectedTab is null || state is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state.FilePath) || !File.Exists(state.FilePath))
        {
            MessageBox.Show(
                this,
                "\u5F53\u524D\u6587\u6863\u5C1A\u672A\u4FDD\u5B58\u5230\u78C1\u76D8\uFF0C\u65E0\u6CD5\u91CD\u65B0\u6253\u5F00\u3002",
                "\u91CD\u65B0\u6253\u5F00",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        SyncActiveDocumentSnapshot();
        if (state.IsDirty)
        {
            var decision = MessageBox.Show(
                this,
                "\u5F53\u524D\u6587\u4EF6\u6709\u672A\u4FDD\u5B58\u4FEE\u6539\uFF0C\u91CD\u65B0\u6253\u5F00\u5C06\u4E22\u5931\u4FEE\u6539\uFF0C\u662F\u5426\u7EE7\u7EED\uFF1F",
                "\u91CD\u65B0\u6253\u5F00",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (decision != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            var readResult = EditorFileEncodingHelper.ReadFileWithEncoding(state.FilePath, encoding, displayName);
            state.TextEncoding = readResult.Encoding;
            state.EncodingDisplayName = readResult.DisplayName;

            LoadEditorDocumentText(
                readResult.Text,
                state.FilePath,
                Path.GetFileName(state.FilePath),
                markClean: true,
                textEncoding: state.TextEncoding,
                encodingDisplayName: state.EncodingDisplayName);

            InvalidateCodeStructureCacheForPath(state.FilePath);
            AppendBuildOutput($"已按 {state.EncodingDisplayName} 重新打开: {state.FilePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "\u91CD\u65B0\u6253\u5F00\u5931\u8D25",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
            SetClipboardUnicodeText(selectedText);
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
            SetClipboardUnicodeText(selectedText);
        }

        editorControlMain.Focus();
    }

    private void PasteInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var clipboardText = GetClipboardTextPreferUnicode();
        if (string.IsNullOrEmpty(clipboardText))
        {
            return;
        }

        editorControlMain.InsertText(clipboardText);
        editorControlMain.Focus();
    }

    private static void SetClipboardUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
        }
        catch
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Ignore clipboard busy failures.
            }
        }
    }

    private static string GetClipboardTextPreferUnicode()
    {
        try
        {
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                return Clipboard.GetText(TextDataFormat.UnicodeText);
            }

            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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

        NavigateToEditorPositionZeroBased(lineNumber - 1, 0);
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
        if (visible)
        {
            ApplyOutputPanelHeight(uiSettings.OutputPanelHeight);
        }

        if (!suppressViewMenuStateSync)
        {
            PersistUiSettingsFromCurrentState();
        }
    }

    private void ResetMainLayout()
    {
        splitWorkspace.Panel1Collapsed = false;
        splitMain.Panel2Collapsed = false;
        splitEditor.Panel2Collapsed = false;

        uiSettings.ExplorerWidth = UiSettings.ExplorerWidthDefault;
        uiSettings.OutputPanelHeight = UiSettings.OutputPanelHeightDefault;
        uiSettings.CodeStructurePanelWidth = UiSettings.CodeStructurePanelWidthDefault;

        splitWorkspace.SplitterDistance = Math.Clamp(uiSettings.ExplorerWidth, ExplorerPanelMinWidth, ExplorerPanelMaxWidth);
        ApplyOutputPanelHeight(uiSettings.OutputPanelHeight);
        ApplyCodeStructurePanelWidth(uiSettings.CodeStructurePanelWidth);
        SyncViewMenuState();
        PersistUiSettingsFromCurrentState();
    }

    private async void ExecuteBuildCommand(string commandId)
    {
        await ExecuteBuildCommandAsync(commandId);
    }

    private void ExecuteDebugCommand(string commandId)
    {
        _ = ExecuteDebugCommandInternalAsync(commandId);
    }

    private void AppendBuildOutput(string message)
    {
        if (rtbRuntimeLog is null)
        {
            return;
        }

        if (rtbRuntimeLog.InvokeRequired)
        {
            rtbRuntimeLog.BeginInvoke(new Action(() => AppendBuildOutput(message)));
            return;
        }

        rtbRuntimeLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        rtbRuntimeLog.SelectionStart = rtbRuntimeLog.TextLength;
        rtbRuntimeLog.ScrollToCaret();
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
        var aboutText = """
            C++Editor
            WinForms 代码编辑器原型

            本软件采用 GPL v3 许可证开源
            https://github.com/LangLang03/Cpp-Editor

            使用的第三方库：
            • OpenSweetEditor - LGPL 许可证
              https://github.com/FinalScave/OpenSweetEditor
            """;
        MessageBox.Show(this, aboutText, "关于 C++Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private void GoToLineInEditor(int lineNumber, int columnNumber = 0)
    {
        if (editorControlMain is null)
        {
            return;
        }

        NavigateToEditorPositionZeroBased(lineNumber - 1, columnNumber);
    }

    private void NavigateToEditorPositionZeroBased(int lineIndex, int columnIndex)
    {
        if (editorControlMain is null)
        {
            return;
        }

        editorControlMain.GotoPosition(Math.Max(0, lineIndex), Math.Max(0, columnIndex));
        editorControlMain.RequestDecorationRefresh();
        editorControlMain.Flush();
        editorControlMain.Focus();
    }

    private void ToggleCodeStructurePanel(bool visible)
    {
        splitEditor.Panel2Collapsed = !visible;
        if (visible)
        {
            ApplyCodeStructurePanelWidth(uiSettings.CodeStructurePanelWidth);
        }

        PersistUiSettingsFromCurrentState();
    }

    private void ShowCodeSnippetDialog()
    {
        using var dialog = new CodeSnippetDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedSnippet is not null)
        {
            InsertCodeSnippet(dialog.SelectedSnippet);
        }
    }

    private void InsertCodeSnippet(CodeSnippet snippet)
    {
        if (editorControlMain is null)
        {
            return;
        }

        var code = CodeSnippetInsertService.ExpandSnippet(snippet.Code);
        var cursorPos = CodeSnippetInsertService.FindCursorPosition(code);
        
        // Remove cursor markers from code
        code = code.Replace("${cursor}", "").Replace("$cursor", "").Replace("|", "").Replace("<|>", "");
        
        editorControlMain.InsertText(code);
        
        // Position cursor
        if (cursorPos >= 0 && cursorPos < code.Length)
        {
            // Calculate line and column from cursor position
            var lines = code.Substring(0, cursorPos).Split('\n');
            var line = lines.Length - 1;
            var column = lines[^1].Length;
            
            // Get current position and add offset
            editorControlMain.GotoPosition(line, column);
        }
        
        editorControlMain.Focus();
    }

    private BuildConfigurationSettings buildConfigurationSettings = BuildConfigurationSettings.CreateDefault();

    private void SetBuildConfiguration(BuildConfiguration configuration)
    {
        buildConfigurationSettings.Configuration = configuration;
        EditorConfigurationController.SaveBuildConfigurationSettings(buildConfigurationSettings);
        
        // Update menu check states
        menuBuildConfigDebug.Checked = configuration == BuildConfiguration.Debug;
        menuBuildConfigRelease.Checked = configuration == BuildConfiguration.Release;
        
        AppendBuildOutput($"构建配置已切换为: {configuration}");
    }
}

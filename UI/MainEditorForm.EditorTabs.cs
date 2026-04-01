namespace C__Editor;

public partial class MainEditorForm
{
    private const int TabCloseButtonSize = 12;
    private const int TabCloseButtonRightPadding = 8;

    private readonly Dictionary<TabPage, EditorDocumentState> editorDocuments = new();
    private readonly CodeStructureAnalyzer codeStructureAnalyzer = new();
    private TabPage? activeEditorTab;
    private int untitledDocumentCounter = 1;

    private sealed class EditorDocumentState
    {
        public string? FilePath { get; set; }

        public string DisplayName { get; set; } = "未命名.cpp";

        public string TextContent { get; set; } = string.Empty;

        public bool IsDirty { get; set; }

        public System.Text.Encoding TextEncoding { get; set; } = EditorFileEncodingHelper.DefaultEncoding;

        public string EncodingDisplayName { get; set; } = "UTF-8";
    }

    private TabControl CreateEditorTabs()
    {
        var editorTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Name = "tabEditorHost",
            TabIndex = 0,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Padding = new Point(20, 6)
        };

        tabEditorHost = editorTabs;
        editorTabs.DrawItem += TabEditorHost_DrawItem;
        editorTabs.MouseDown += TabEditorHost_MouseDown;
        editorTabs.SelectedIndexChanged += TabEditorHost_SelectedIndexChanged;

        var editorHostControl = BuildEditorControlHost();
        if (editorHostControl is SweetEditor.EditorControl)
        {
            var firstTab = CreateDocumentTab("// Ready\n", null, "未命名.cpp", markClean: true);
            editorTabs.SelectedTab = firstTab;
            ActivateDocumentTab(firstTab);
        }
        else
        {
            var fallbackTab = new TabPage
            {
                Text = "编辑器",
                Padding = new Padding(0),
                UseVisualStyleBackColor = false,
                BackColor = treeProject.BackColor,
                ForeColor = ForeColor
            };

            fallbackTab.Controls.Add(editorHostControl);
            editorTabs.TabPages.Add(fallbackTab);
        }

        return editorTabs;
    }

    private Control BuildEditorControlHost()
    {
        try
        {
            editorControlMain = new SweetEditor.EditorControl
            {
                Dock = DockStyle.Fill,
                Name = "editorControlMain",
                Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point, 0),
                TabIndex = 0
            };

            EditorThemeController.ApplyTheme(uiSettings.ThemeId, editorControlMain);
            InitializeSyntaxHighlighting();
            AttachEditorEventHandlers();
            InitializeEditorContextMenu();
            InitializeBreakpointMarkerSupport();
            return editorControlMain;
        }
        catch (Exception ex)
        {
            var fallbackLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 134),
                ForeColor = Color.DarkRed,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "编辑器加载失败\r\n" + ex.Message
            };

            return fallbackLabel;
        }
    }

    private TabPage CreateDocumentTab(
        string text,
        string? filePath,
        string? displayName,
        bool markClean,
        System.Text.Encoding? textEncoding = null,
        string? encodingDisplayName = null)
    {
        var effectiveEncoding = textEncoding ?? EditorFileEncodingHelper.DefaultEncoding;
        var state = new EditorDocumentState
        {
            FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath),
            DisplayName = ResolveDocumentDisplayName(filePath, displayName),
            TextContent = NormalizeEditorNewlines(text),
            IsDirty = !markClean,
            TextEncoding = effectiveEncoding,
            EncodingDisplayName = ResolveEncodingDisplayName(effectiveEncoding, encodingDisplayName)
        };

        var tabPage = new TabPage
        {
            Padding = new Padding(0),
            UseVisualStyleBackColor = false,
            BackColor = treeProject.BackColor,
            ForeColor = ForeColor,
            Tag = state
        };

        editorDocuments[tabPage] = state;
        tabEditorHost.TabPages.Add(tabPage);
        UpdateEditorTabHeader(targetTab: tabPage);
        return tabPage;
    }

    private string BuildUntitledName()
    {
        string name;
        do
        {
            name = untitledDocumentCounter <= 1
                ? "未命名.cpp"
                : $"未命名{untitledDocumentCounter}.cpp";

            untitledDocumentCounter++;
        }
        while (editorDocuments.Values.Any(state =>
            string.IsNullOrWhiteSpace(state.FilePath) &&
            string.Equals(state.DisplayName, name, StringComparison.OrdinalIgnoreCase)));

        return name;
    }

    private static string ResolveDocumentDisplayName(string? filePath, string? displayName = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return Path.GetFileName(filePath);
        }

        return string.IsNullOrWhiteSpace(displayName) ? "未命名.cpp" : displayName;
    }

    private EditorDocumentState? GetDocumentState(TabPage? tab)
    {
        if (tab is null)
        {
            return null;
        }

        return editorDocuments.TryGetValue(tab, out var state)
            ? state
            : tab.Tag as EditorDocumentState;
    }

    private EditorDocumentState? GetSelectedDocumentState()
    {
        return GetDocumentState(tabEditorHost?.SelectedTab);
    }

    private void SyncActiveDocumentSnapshot()
    {
        if (activeEditorTab is null || editorControlMain is null)
        {
            return;
        }

        var state = GetDocumentState(activeEditorTab);
        if (state is null)
        {
            return;
        }

        state.TextContent = GetEditorText();
        state.FilePath = currentEditorFilePath;
        state.IsDirty = hasUnsavedChanges;
        state.DisplayName = ResolveDocumentDisplayName(state.FilePath, state.DisplayName);
        UpdateEditorTabHeader(targetTab: activeEditorTab);
    }

    private void ActivateDocumentTab(TabPage? tab)
    {
        if (tab is null || editorControlMain is null)
        {
            return;
        }

        var state = GetDocumentState(tab);
        if (state is null)
        {
            return;
        }

        if (activeEditorTab == tab && ReferenceEquals(editorControlMain.Parent, tab))
        {
            UpdateEditorStatusBar();
            return;
        }

        SyncActiveDocumentSnapshot();
        activeEditorTab = tab;

        if (!ReferenceEquals(editorControlMain.Parent, tab))
        {
            tab.Controls.Add(editorControlMain);
            editorControlMain.Dock = DockStyle.Fill;
        }

        isLoadingEditorDocument = true;
        try
        {
            currentEditorFilePath = state.FilePath;
            hasUnsavedChanges = state.IsDirty;

            var syntaxSource = state.FilePath ?? state.DisplayName;
            ApplyEditorLanguageConfiguration(syntaxSource);
            SetEditorSyntaxSource(syntaxSource, state.TextContent);
            editorControlMain.LoadDocument(new SweetEditor.Document(state.TextContent));
            editorControlMain.RequestDecorationRefresh();
            ApplyBreakpointMarkersForCurrentDocument();
            UpdateEditorTabHeader(targetTab: tab);
        }
        finally
        {
            isLoadingEditorDocument = false;
        }

        // Update code structure browser when tab is activated
        UpdateCodeStructureBrowser();
        UpdateEditorStatusBar();
    }

    private TabPage? FindTabByFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        foreach (var pair in editorDocuments)
        {
            var existingPath = pair.Value.FilePath;
            if (string.IsNullOrWhiteSpace(existingPath))
            {
                continue;
            }

            if (string.Equals(Path.GetFullPath(existingPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    private void OpenFileInEditorTab(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var existing = FindTabByFilePath(normalizedPath);
        if (existing is not null)
        {
            tabEditorHost.SelectedTab = existing;
            ActivateDocumentTab(existing);
            editorControlMain?.Focus();
            return;
        }

        var readResult = EditorFileEncodingHelper.ReadFileWithDetectedEncoding(normalizedPath);
        var tab = CreateDocumentTab(
            readResult.Text,
            normalizedPath,
            Path.GetFileName(normalizedPath),
            markClean: true,
            textEncoding: readResult.Encoding,
            encodingDisplayName: readResult.DisplayName);
        tabEditorHost.SelectedTab = tab;
        ActivateDocumentTab(tab);
        editorControlMain?.Focus();
    }

    private bool EnsureCanCloseAllDocuments(bool closeTabs)
    {
        if (tabEditorHost is null || tabEditorHost.TabPages.Count == 0)
        {
            return true;
        }

        var tabs = tabEditorHost.TabPages.Cast<TabPage>().ToList();
        foreach (var tab in tabs)
        {
            if (closeTabs)
            {
                if (!TryCloseEditorTab(tab, createFallbackIfEmpty: false))
                {
                    return false;
                }

                continue;
            }

            if (!ConfirmCloseForTab(tab))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryCloseEditorTab(TabPage? tab, bool createFallbackIfEmpty)
    {
        if (tab is null || tabEditorHost is null)
        {
            return false;
        }

        if (!ConfirmCloseForTab(tab))
        {
            return false;
        }

        var wasActive = tab == activeEditorTab;
        var wasSelected = tab == tabEditorHost.SelectedTab;
        var currentIndex = tabEditorHost.TabPages.IndexOf(tab);
        var nextIndex = Math.Max(0, currentIndex - 1);

        if (ReferenceEquals(editorControlMain?.Parent, tab))
        {
            tab.Controls.Remove(editorControlMain);
        }

        editorDocuments.Remove(tab);
        tabEditorHost.TabPages.Remove(tab);
        tab.Dispose();

        if (tabEditorHost.TabPages.Count == 0)
        {
            activeEditorTab = null;
            currentEditorFilePath = null;
            hasUnsavedChanges = false;
            UpdateEditorStatusBar();

            if (createFallbackIfEmpty)
            {
                var fallbackTab = CreateDocumentTab("// Ready\n", null, BuildUntitledName(), markClean: true);
                tabEditorHost.SelectedTab = fallbackTab;
                ActivateDocumentTab(fallbackTab);
            }

            return true;
        }

        if (wasActive || wasSelected || tabEditorHost.SelectedTab is null)
        {
            nextIndex = Math.Min(nextIndex, tabEditorHost.TabPages.Count - 1);
            tabEditorHost.SelectedIndex = nextIndex;
            ActivateDocumentTab(tabEditorHost.SelectedTab);
        }

        return true;
    }

    private bool ConfirmCloseForTab(TabPage tab)
    {
        var state = GetDocumentState(tab);
        if (state is null)
        {
            return true;
        }

        if (tab == activeEditorTab)
        {
            SyncActiveDocumentSnapshot();
        }

        if (!state.IsDirty)
        {
            return true;
        }

        var displayName = ResolveDocumentDisplayName(state.FilePath, state.DisplayName);
        var result = MessageBox.Show(
            this,
            $"文件 \"{displayName}\" 尚未保存，是否先保存？",
            "未保存更改",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Cancel)
        {
            return false;
        }

        if (result == DialogResult.No)
        {
            return true;
        }

        var previousTab = tabEditorHost.SelectedTab;
        if (previousTab != tab)
        {
            tabEditorHost.SelectedTab = tab;
            ActivateDocumentTab(tab);
        }

        var saved = SaveCurrentDocument();
        if (!saved && previousTab is not null && tabEditorHost.TabPages.Contains(previousTab))
        {
            tabEditorHost.SelectedTab = previousTab;
            ActivateDocumentTab(previousTab);
        }

        return saved;
    }

    private Rectangle GetTabCloseButtonBounds(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= tabEditorHost.TabPages.Count)
        {
            return Rectangle.Empty;
        }

        var tabRect = tabEditorHost.GetTabRect(tabIndex);
        var x = tabRect.Right - TabCloseButtonRightPadding - TabCloseButtonSize;
        var y = tabRect.Top + ((tabRect.Height - TabCloseButtonSize) / 2);
        return new Rectangle(x, y, TabCloseButtonSize, TabCloseButtonSize);
    }

    private void TabEditorHost_SelectedIndexChanged(object? sender, EventArgs e)
    {
        ActivateDocumentTab(tabEditorHost.SelectedTab);
    }

    private void UpdateCodeStructureBrowser(bool forceRefresh = false)
    {
        if (codeStructureBrowser is null)
        {
            return;
        }

        var state = GetSelectedDocumentState();
        if (state?.FilePath is null || !File.Exists(state.FilePath))
        {
            codeStructureBrowser.Clear();
            return;
        }

        var filePath = Path.GetFullPath(state.FilePath);
        codeStructureBrowser.SetCurrentFile(filePath);

        var shouldAnalyzeNow = forceRefresh || codeStructureSettings.AutoRefresh;
        if (!shouldAnalyzeNow)
        {
            if (codeStructureAnalyzer.TryGetCachedResult(filePath, out var cachedResult) && cachedResult is not null)
            {
                codeStructureBrowser.ShowResult(cachedResult);
            }
            else
            {
                codeStructureBrowser.ShowStatusMessage("自动分析已关闭，请点击刷新。");
            }

            return;
        }

        codeStructureBrowser.ShowStatusMessage("正在分析...");

        var content = ResolveCodeStructureContent(state);
        var result = codeStructureAnalyzer.Analyze(filePath, content, forceRefresh);
        codeStructureBrowser.ShowResult(result);
    }

    private string ResolveCodeStructureContent(EditorDocumentState state)
    {
        if (activeEditorTab == tabEditorHost.SelectedTab && editorControlMain is not null)
        {
            state.TextContent = GetEditorText();
            return state.TextContent;
        }

        if (state.IsDirty)
        {
            return state.TextContent;
        }

        if (!string.IsNullOrEmpty(state.TextContent))
        {
            return state.TextContent;
        }

        if (string.IsNullOrWhiteSpace(state.FilePath) || !File.Exists(state.FilePath))
        {
            return string.Empty;
        }

        return EditorFileEncodingHelper.ReadFileWithEncoding(
            state.FilePath,
            state.TextEncoding,
            state.EncodingDisplayName).Text;
    }

    private void RefreshCodeStructureBrowser()
    {
        UpdateCodeStructureBrowser(forceRefresh: true);
    }

    private void InvalidateCodeStructureCacheForCurrentFile()
    {
        var state = GetSelectedDocumentState();
        if (!string.IsNullOrWhiteSpace(state?.FilePath))
        {
            codeStructureAnalyzer.Invalidate(state.FilePath);
        }
    }

    private void InvalidateCodeStructureCacheForPath(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            codeStructureAnalyzer.Invalidate(filePath);
        }
    }

    private void InvalidateCodeStructureCacheForTab(TabPage? tab)
    {
        var state = GetDocumentState(tab);
        if (!string.IsNullOrWhiteSpace(state?.FilePath))
        {
            codeStructureAnalyzer.Invalidate(state.FilePath);
        }
    }

    private void TabEditorHost_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        for (var i = 0; i < tabEditorHost.TabPages.Count; i++)
        {
            if (!GetTabCloseButtonBounds(i).Contains(e.Location))
            {
                continue;
            }

            var tab = tabEditorHost.TabPages[i];
            tabEditorHost.SelectedTab = tab;
            CloseCurrentDocument();
            return;
        }
    }

    private void TabEditorHost_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= tabEditorHost.TabPages.Count)
        {
            return;
        }

        var tab = tabEditorHost.TabPages[e.Index];
        var bounds = e.Bounds;
        var selected = e.Index == tabEditorHost.SelectedIndex;
        var selectedBackColor = tab.BackColor == Color.Empty ? tabEditorHost.BackColor : tab.BackColor;
        var unselectedBackColor = BlendColor(tabEditorHost.BackColor, selectedBackColor, 0.55f);
        var tabTextColor = tabEditorHost.ForeColor;
        var borderColor = splitWorkspace.BackColor;

        using (var backgroundBrush = new SolidBrush(selected ? selectedBackColor : unselectedBackColor))
        {
            e.Graphics.FillRectangle(backgroundBrush, bounds);
        }

        var closeBounds = GetTabCloseButtonBounds(e.Index);
        var textBounds = new Rectangle(
            bounds.X + 8,
            bounds.Y + 2,
            Math.Max(10, closeBounds.Left - bounds.X - 12),
            bounds.Height - 4);

        TextRenderer.DrawText(
            e.Graphics,
            tab.Text,
            tabEditorHost.Font,
            textBounds,
            tabTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using (var closePen = new Pen(tabTextColor, 1.6f))
        {
            e.Graphics.DrawLine(closePen, closeBounds.Left + 3, closeBounds.Top + 3, closeBounds.Right - 3, closeBounds.Bottom - 3);
            e.Graphics.DrawLine(closePen, closeBounds.Right - 3, closeBounds.Top + 3, closeBounds.Left + 3, closeBounds.Bottom - 3);
        }

        using var borderPen = new Pen(borderColor);
        e.Graphics.DrawRectangle(borderPen, bounds);
    }

    private static string ResolveEncodingDisplayName(System.Text.Encoding encoding, string? encodingDisplayName)
    {
        return string.IsNullOrWhiteSpace(encodingDisplayName)
            ? EditorFileEncodingHelper.GetDisplayName(encoding)
            : encodingDisplayName;
    }

    private static Color BlendColor(Color baseColor, Color overlayColor, double overlayRatio)
    {
        var ratio = Math.Clamp(overlayRatio, 0d, 1d);
        var inverse = 1d - ratio;
        return Color.FromArgb(
            (int)Math.Round(baseColor.R * inverse + overlayColor.R * ratio),
            (int)Math.Round(baseColor.G * inverse + overlayColor.G * ratio),
            (int)Math.Round(baseColor.B * inverse + overlayColor.B * ratio));
    }
}

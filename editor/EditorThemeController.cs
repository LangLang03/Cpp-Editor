namespace C__Editor;

internal static class EditorThemeController
{
    internal const string LightThemeId = "light";
    internal const string DarkThemeId = "dark";

    private const int FontStyleRegular = 0;
    private const int FontStyleBold = SweetEditor.EditorControl.FONT_STYLE_BOLD;
    private const int FontStyleItalic = SweetEditor.EditorControl.FONT_STYLE_ITALIC;

    private static readonly ThemePalette LightPalette = new()
    {
        UseDarkEditorPreset = false,
        AppBackgroundColor = Color.FromArgb(unchecked((int)0xFFF3F6FB)),
        SurfaceColor = Color.FromArgb(unchecked((int)0xFFFFFFFF)),
        SurfaceAltColor = Color.FromArgb(unchecked((int)0xFFF8FAFC)),
        HeaderColor = Color.FromArgb(unchecked((int)0xFFECF2F8)),
        BorderColor = Color.FromArgb(unchecked((int)0xFFD8E1EC)),
        PrimaryTextColor = Color.FromArgb(unchecked((int)0xFF1F2937)),
        SelectionAccentColor = Color.FromArgb(unchecked((int)0xFFDBEAFE)),
        EditorBackgroundColor = Color.FromArgb(unchecked((int)0xFFF7FAFC)),
        EditorTextColor = Color.FromArgb(unchecked((int)0xFF1F2937)),
        EditorCursorColor = Color.FromArgb(unchecked((int)0xFF1D4ED8)),
        EditorSelectionColor = Color.FromArgb(unchecked((int)0x553B82F6)),
        EditorLineNumberColor = Color.FromArgb(unchecked((int)0xFF94A3B8)),
        EditorCurrentLineNumberColor = Color.FromArgb(unchecked((int)0xFF475569)),
        EditorCurrentLineColor = Color.FromArgb(unchecked((int)0x153B82F6)),
        EditorGuideColor = Color.FromArgb(unchecked((int)0x222E3A59)),
        EditorSeparatorColor = Color.FromArgb(unchecked((int)0xFF2F855A)),
        EditorSplitLineColor = Color.FromArgb(unchecked((int)0x223B82F6)),
        EditorCompositionColor = Color.FromArgb(unchecked((int)0xFF2563EB)),
        KeywordColor = unchecked((int)0xFF1D4ED8),
        StringColor = unchecked((int)0xFF0F766E),
        CommentColor = unchecked((int)0xFF64748B),
        NumberColor = unchecked((int)0xFFD97706),
        TypeColor = unchecked((int)0xFF7C3AED),
        FunctionColor = unchecked((int)0xFF0E7490),
        PreprocessorColor = unchecked((int)0xFFB91C1C)
    };

    private static readonly ThemePalette DarkPalette = new()
    {
        UseDarkEditorPreset = true,
        AppBackgroundColor = Color.FromArgb(unchecked((int)0xFF1E1E1E)),
        SurfaceColor = Color.FromArgb(unchecked((int)0xFF252526)),
        SurfaceAltColor = Color.FromArgb(unchecked((int)0xFF2D2D30)),
        HeaderColor = Color.FromArgb(unchecked((int)0xFF2D2D30)),
        BorderColor = Color.FromArgb(unchecked((int)0xFF3C3C3C)),
        PrimaryTextColor = Color.FromArgb(unchecked((int)0xFFD4D4D4)),
        SelectionAccentColor = Color.FromArgb(unchecked((int)0xFF264F78)),
        EditorBackgroundColor = Color.FromArgb(unchecked((int)0xFF1E1E1E)),
        EditorTextColor = Color.FromArgb(unchecked((int)0xFFD4D4D4)),
        EditorCursorColor = Color.FromArgb(unchecked((int)0xFFAEAFAD)),
        EditorSelectionColor = Color.FromArgb(unchecked((int)0x5532648A)),
        EditorLineNumberColor = Color.FromArgb(unchecked((int)0xFF858585)),
        EditorCurrentLineNumberColor = Color.FromArgb(unchecked((int)0xFFC6C6C6)),
        EditorCurrentLineColor = Color.FromArgb(unchecked((int)0x302A2D2E)),
        EditorGuideColor = Color.FromArgb(unchecked((int)0x33404040)),
        EditorSeparatorColor = Color.FromArgb(unchecked((int)0xFF3C3C3C)),
        EditorSplitLineColor = Color.FromArgb(unchecked((int)0x33404040)),
        EditorCompositionColor = Color.FromArgb(unchecked((int)0xFF4FC1FF)),
        KeywordColor = unchecked((int)0xFF569CD6),
        StringColor = unchecked((int)0xFFCE9178),
        CommentColor = unchecked((int)0xFF6A9955),
        NumberColor = unchecked((int)0xFFB5CEA8),
        TypeColor = unchecked((int)0xFF4EC9B0),
        FunctionColor = unchecked((int)0xFFDCDCAA),
        PreprocessorColor = unchecked((int)0xFFC586C0)
    };

    internal static string NormalizeThemeId(string? themeId)
    {
        return string.Equals(themeId, DarkThemeId, StringComparison.OrdinalIgnoreCase)
            ? DarkThemeId
            : LightThemeId;
    }

    internal static void ApplyTheme(
        string? themeId,
        MainEditorForm form,
        MenuStrip menuMain,
        SplitContainer splitMain,
        SplitContainer splitWorkspace,
        TreeView treeProject,
        TabControl tabEditorHost,
        TabControl tabBottom,
        RichTextBox rtbBuildOutput,
        DataGridView dgvCompileErrors,
        RichTextBox rtbRunOutput,
        RichTextBox rtbRuntimeLog,
        StatusStrip? statusEditor = null)
    {
        var palette = ResolvePalette(themeId);

        form.BackColor = palette.AppBackgroundColor;
        form.ForeColor = palette.PrimaryTextColor;

        ApplyMenuTheme(menuMain, palette);

        splitMain.BackColor = palette.BorderColor;
        splitWorkspace.BackColor = palette.BorderColor;
        splitMain.Panel1.BackColor = palette.AppBackgroundColor;
        splitMain.Panel2.BackColor = palette.AppBackgroundColor;
        splitWorkspace.Panel1.BackColor = palette.SurfaceColor;
        splitWorkspace.Panel2.BackColor = palette.SurfaceColor;

        treeProject.BackColor = palette.SurfaceColor;
        treeProject.ForeColor = palette.PrimaryTextColor;
        treeProject.LineColor = palette.BorderColor;

        ApplyTabControlTheme(tabEditorHost, palette);
        ApplyTabControlTheme(tabBottom, palette);

        ApplyOutputTheme(rtbBuildOutput, palette);
        ApplyOutputTheme(rtbRunOutput, palette);
        ApplyOutputTheme(rtbRuntimeLog, palette);
        ApplyCompileErrorsTheme(dgvCompileErrors, palette);
        ApplyStatusStripTheme(statusEditor, palette);

        ApplyContextMenuTheme(treeProject.ContextMenuStrip, palette);
        ApplyContextMenuTheme(rtbBuildOutput.ContextMenuStrip, palette);
        ApplyContextMenuTheme(rtbRunOutput.ContextMenuStrip, palette);
        ApplyContextMenuTheme(rtbRuntimeLog.ContextMenuStrip, palette);
        ApplyContextMenuTheme(dgvCompileErrors.ContextMenuStrip, palette);

        tabEditorHost.Invalidate();
        treeProject.Invalidate();
    }

    internal static void ApplyTheme(string? themeId, SweetEditor.EditorControl editorControl)
    {
        var palette = ResolvePalette(themeId);
        var theme = CreateTheme(palette);
        editorControl.ApplyTheme(theme);
        editorControl.Settings.SetCurrentLineRenderMode(SweetEditor.CurrentLineRenderMode.BORDER);
    }

    internal static void ApplyLightTheme(
        MainEditorForm form,
        MenuStrip menuMain,
        SplitContainer splitMain,
        SplitContainer splitWorkspace,
        TreeView treeProject,
        TabControl tabEditorHost,
        TabControl tabBottom,
        RichTextBox rtbBuildOutput,
        DataGridView dgvCompileErrors,
        RichTextBox rtbRunOutput,
        RichTextBox rtbRuntimeLog,
        StatusStrip? statusEditor = null)
    {
        ApplyTheme(
            LightThemeId,
            form,
            menuMain,
            splitMain,
            splitWorkspace,
            treeProject,
            tabEditorHost,
            tabBottom,
            rtbBuildOutput,
            dgvCompileErrors,
            rtbRunOutput,
            rtbRuntimeLog,
            statusEditor);
    }

    internal static void ApplyLightTheme(SweetEditor.EditorControl editorControl)
    {
        ApplyTheme(LightThemeId, editorControl);
    }

    private static ThemePalette ResolvePalette(string? themeId)
    {
        return string.Equals(NormalizeThemeId(themeId), DarkThemeId, StringComparison.Ordinal)
            ? DarkPalette
            : LightPalette;
    }

    private static SweetEditor.EditorTheme CreateTheme(ThemePalette palette)
    {
        var theme = palette.UseDarkEditorPreset ? SweetEditor.EditorTheme.Dark() : SweetEditor.EditorTheme.Light();

        theme.BackgroundColor = palette.EditorBackgroundColor;
        theme.TextColor = palette.EditorTextColor;
        theme.CursorColor = palette.EditorCursorColor;
        theme.SelectionColor = palette.EditorSelectionColor;
        theme.LineNumberColor = palette.EditorLineNumberColor;
        theme.CurrentLineNumberColor = palette.EditorCurrentLineNumberColor;
        theme.CurrentLineColor = palette.EditorCurrentLineColor;
        theme.GuideColor = palette.EditorGuideColor;
        theme.SeparatorColor = palette.EditorSeparatorColor;
        theme.SplitLineColor = palette.EditorSplitLineColor;
        theme.CompositionColor = palette.EditorCompositionColor;

        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_KEYWORD, new SweetEditor.TextStyle(palette.KeywordColor, FontStyleBold));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_STRING, new SweetEditor.TextStyle(palette.StringColor, FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_COMMENT, new SweetEditor.TextStyle(palette.CommentColor, FontStyleItalic));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_NUMBER, new SweetEditor.TextStyle(palette.NumberColor, FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_TYPE, new SweetEditor.TextStyle(palette.TypeColor, FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_FUNCTION, new SweetEditor.TextStyle(palette.FunctionColor, FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_PREPROCESSOR, new SweetEditor.TextStyle(palette.PreprocessorColor, FontStyleRegular));

        return theme;
    }

    private static void ApplyMenuTheme(MenuStrip menuMain, ThemePalette palette)
    {
        menuMain.BackColor = palette.HeaderColor;
        menuMain.ForeColor = palette.PrimaryTextColor;
        menuMain.Renderer = new ToolStripProfessionalRenderer(new ThemeMenuColorTable(palette));
    }

    private static void ApplyTabControlTheme(TabControl tabControl, ThemePalette palette)
    {
        tabControl.BackColor = palette.AppBackgroundColor;
        tabControl.ForeColor = palette.PrimaryTextColor;
        tabControl.Appearance = TabAppearance.Normal;

        foreach (TabPage tabPage in tabControl.TabPages)
        {
            tabPage.UseVisualStyleBackColor = false;
            tabPage.BackColor = palette.SurfaceColor;
            tabPage.ForeColor = palette.PrimaryTextColor;
        }
    }

    private static void ApplyOutputTheme(RichTextBox outputBox, ThemePalette palette)
    {
        outputBox.BackColor = palette.SurfaceAltColor;
        outputBox.ForeColor = palette.PrimaryTextColor;

        if (outputBox.TextLength <= 0)
        {
            return;
        }

        var selectionStart = outputBox.SelectionStart;
        var selectionLength = outputBox.SelectionLength;
        outputBox.SelectAll();
        outputBox.SelectionColor = palette.PrimaryTextColor;
        outputBox.SelectionBackColor = palette.SurfaceAltColor;
        outputBox.Select(selectionStart, selectionLength);
    }

    private static void ApplyCompileErrorsTheme(DataGridView grid, ThemePalette palette)
    {
        grid.BackgroundColor = palette.SurfaceColor;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = palette.BorderColor;
        grid.EnableHeadersVisualStyles = false;

        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.PrimaryTextColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.PrimaryTextColor;

        grid.DefaultCellStyle.BackColor = palette.SurfaceColor;
        grid.DefaultCellStyle.ForeColor = palette.PrimaryTextColor;
        grid.DefaultCellStyle.SelectionBackColor = palette.SelectionAccentColor;
        grid.DefaultCellStyle.SelectionForeColor = palette.PrimaryTextColor;

        grid.AlternatingRowsDefaultCellStyle.BackColor = palette.SurfaceAltColor;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = palette.PrimaryTextColor;

        foreach (DataGridViewRow row in grid.Rows)
        {
            row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                ? palette.SurfaceColor
                : palette.SurfaceAltColor;
            row.DefaultCellStyle.ForeColor = palette.PrimaryTextColor;
            row.DefaultCellStyle.SelectionBackColor = palette.SelectionAccentColor;
            row.DefaultCellStyle.SelectionForeColor = palette.PrimaryTextColor;
        }
    }

    private static void ApplyStatusStripTheme(StatusStrip? statusStrip, ThemePalette palette)
    {
        if (statusStrip is null)
        {
            return;
        }

        statusStrip.BackColor = palette.HeaderColor;
        statusStrip.ForeColor = palette.PrimaryTextColor;

        foreach (ToolStripItem item in statusStrip.Items)
        {
            item.BackColor = palette.HeaderColor;
            item.ForeColor = palette.PrimaryTextColor;
        }
    }

    private static void ApplyContextMenuTheme(ContextMenuStrip? menu, ThemePalette palette)
    {
        if (menu is null)
        {
            return;
        }

        menu.BackColor = palette.SurfaceColor;
        menu.ForeColor = palette.PrimaryTextColor;
        menu.Renderer = new ToolStripProfessionalRenderer(new ThemeMenuColorTable(palette));

        foreach (ToolStripItem item in menu.Items)
        {
            ApplyToolStripItemTheme(item, palette);
        }
    }

    private static void ApplyToolStripItemTheme(ToolStripItem item, ThemePalette palette)
    {
        item.BackColor = palette.SurfaceColor;
        item.ForeColor = palette.PrimaryTextColor;

        if (item is not ToolStripMenuItem menuItem)
        {
            return;
        }

        menuItem.DropDown.BackColor = palette.SurfaceColor;
        menuItem.DropDown.ForeColor = palette.PrimaryTextColor;
        foreach (ToolStripItem child in menuItem.DropDownItems)
        {
            ApplyToolStripItemTheme(child, palette);
        }
    }

    private sealed class ThemeMenuColorTable(ThemePalette palette) : ProfessionalColorTable
    {
        public override Color MenuBorder => palette.BorderColor;
        public override Color MenuItemBorder => palette.BorderColor;
        public override Color MenuItemSelected => palette.SelectionAccentColor;
        public override Color MenuItemSelectedGradientBegin => palette.SelectionAccentColor;
        public override Color MenuItemSelectedGradientEnd => palette.SelectionAccentColor;
        public override Color MenuStripGradientBegin => palette.HeaderColor;
        public override Color MenuStripGradientEnd => palette.HeaderColor;
        public override Color ToolStripDropDownBackground => palette.SurfaceColor;
        public override Color ImageMarginGradientBegin => palette.SurfaceColor;
        public override Color ImageMarginGradientMiddle => palette.SurfaceColor;
        public override Color ImageMarginGradientEnd => palette.SurfaceColor;
    }

    private sealed class ThemePalette
    {
        public bool UseDarkEditorPreset { get; init; }
        public Color AppBackgroundColor { get; init; }
        public Color SurfaceColor { get; init; }
        public Color SurfaceAltColor { get; init; }
        public Color HeaderColor { get; init; }
        public Color BorderColor { get; init; }
        public Color PrimaryTextColor { get; init; }
        public Color SelectionAccentColor { get; init; }
        public Color EditorBackgroundColor { get; init; }
        public Color EditorTextColor { get; init; }
        public Color EditorCursorColor { get; init; }
        public Color EditorSelectionColor { get; init; }
        public Color EditorLineNumberColor { get; init; }
        public Color EditorCurrentLineNumberColor { get; init; }
        public Color EditorCurrentLineColor { get; init; }
        public Color EditorGuideColor { get; init; }
        public Color EditorSeparatorColor { get; init; }
        public Color EditorSplitLineColor { get; init; }
        public Color EditorCompositionColor { get; init; }
        public int KeywordColor { get; init; }
        public int StringColor { get; init; }
        public int CommentColor { get; init; }
        public int NumberColor { get; init; }
        public int TypeColor { get; init; }
        public int FunctionColor { get; init; }
        public int PreprocessorColor { get; init; }
    }
}

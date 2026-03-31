namespace C__Editor;

internal static class EditorThemeController
{
    private const int FontStyleRegular = 0;
    private const int FontStyleBold = SweetEditor.EditorControl.FONT_STYLE_BOLD;
    private const int FontStyleItalic = SweetEditor.EditorControl.FONT_STYLE_ITALIC;

    private static readonly Color AppBackgroundColor = Color.FromArgb(unchecked((int)0xFFF3F6FB));
    private static readonly Color SurfaceColor = Color.FromArgb(unchecked((int)0xFFFFFFFF));
    private static readonly Color SurfaceAltColor = Color.FromArgb(unchecked((int)0xFFF8FAFC));
    private static readonly Color HeaderColor = Color.FromArgb(unchecked((int)0xFFECF2F8));
    private static readonly Color BorderColor = Color.FromArgb(unchecked((int)0xFFD8E1EC));
    private static readonly Color PrimaryTextColor = Color.FromArgb(unchecked((int)0xFF1F2937));
    private static readonly Color SelectionAccentColor = Color.FromArgb(unchecked((int)0xFFDBEAFE));

    private static readonly Color BackgroundColor = Color.FromArgb(unchecked((int)0xFFF7FAFC));
    private static readonly Color TextColor = Color.FromArgb(unchecked((int)0xFF1F2937));
    private static readonly Color CursorColor = Color.FromArgb(unchecked((int)0xFF1D4ED8));
    private static readonly Color SelectionColor = Color.FromArgb(unchecked((int)0x553B82F6));
    private static readonly Color LineNumberColor = Color.FromArgb(unchecked((int)0xFF94A3B8));
    private static readonly Color CurrentLineNumberColor = Color.FromArgb(unchecked((int)0xFF475569));
    private static readonly Color CurrentLineColor = Color.FromArgb(unchecked((int)0x153B82F6));
    private static readonly Color GuideColor = Color.FromArgb(unchecked((int)0x222E3A59));
    private static readonly Color SeparatorColor = Color.FromArgb(unchecked((int)0xFF2F855A));
    private static readonly Color SplitLineColor = Color.FromArgb(unchecked((int)0x223B82F6));
    private static readonly Color CompositionColor = Color.FromArgb(unchecked((int)0xFF2563EB));

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
        RichTextBox rtbRunOutput)
    {
        form.BackColor = AppBackgroundColor;
        form.ForeColor = PrimaryTextColor;

        ApplyMenuTheme(menuMain);

        splitMain.BackColor = BorderColor;
        splitWorkspace.BackColor = BorderColor;

        treeProject.BackColor = SurfaceColor;
        treeProject.ForeColor = PrimaryTextColor;
        treeProject.LineColor = BorderColor;

        ApplyTabControlTheme(tabEditorHost);
        ApplyTabControlTheme(tabBottom);

        ApplyOutputTheme(rtbBuildOutput);
        ApplyOutputTheme(rtbRunOutput);
        ApplyCompileErrorsTheme(dgvCompileErrors);
    }

    internal static void ApplyLightTheme(SweetEditor.EditorControl editorControl)
    {
        var theme = CreateLightTheme();
        editorControl.ApplyTheme(theme);
        editorControl.Settings.SetCurrentLineRenderMode(SweetEditor.CurrentLineRenderMode.BORDER);
    }

    private static SweetEditor.EditorTheme CreateLightTheme()
    {
        var theme = SweetEditor.EditorTheme.Light();

        theme.BackgroundColor = BackgroundColor;
        theme.TextColor = TextColor;
        theme.CursorColor = CursorColor;
        theme.SelectionColor = SelectionColor;
        theme.LineNumberColor = LineNumberColor;
        theme.CurrentLineNumberColor = CurrentLineNumberColor;
        theme.CurrentLineColor = CurrentLineColor;
        theme.GuideColor = GuideColor;
        theme.SeparatorColor = SeparatorColor;
        theme.SplitLineColor = SplitLineColor;
        theme.CompositionColor = CompositionColor;

        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_KEYWORD, new SweetEditor.TextStyle(unchecked((int)0xFF1D4ED8), FontStyleBold));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_STRING, new SweetEditor.TextStyle(unchecked((int)0xFF0F766E), FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_COMMENT, new SweetEditor.TextStyle(unchecked((int)0xFF64748B), FontStyleItalic));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_NUMBER, new SweetEditor.TextStyle(unchecked((int)0xFFD97706), FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_TYPE, new SweetEditor.TextStyle(unchecked((int)0xFF7C3AED), FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_FUNCTION, new SweetEditor.TextStyle(unchecked((int)0xFF0E7490), FontStyleRegular));
        theme.DefineTextStyle(SweetEditor.EditorTheme.STYLE_PREPROCESSOR, new SweetEditor.TextStyle(unchecked((int)0xFFB91C1C), FontStyleRegular));

        return theme;
    }

    private static void ApplyMenuTheme(MenuStrip menuMain)
    {
        menuMain.BackColor = HeaderColor;
        menuMain.ForeColor = PrimaryTextColor;
        menuMain.Renderer = new ToolStripProfessionalRenderer(new LightMenuColorTable());
    }

    private static void ApplyTabControlTheme(TabControl tabControl)
    {
        tabControl.BackColor = AppBackgroundColor;
        tabControl.ForeColor = PrimaryTextColor;

        foreach (TabPage tabPage in tabControl.TabPages)
        {
            tabPage.UseVisualStyleBackColor = false;
            tabPage.BackColor = SurfaceColor;
            tabPage.ForeColor = PrimaryTextColor;
        }
    }

    private static void ApplyOutputTheme(RichTextBox outputBox)
    {
        outputBox.BackColor = SurfaceAltColor;
        outputBox.ForeColor = PrimaryTextColor;
    }

    private static void ApplyCompileErrorsTheme(DataGridView grid)
    {
        grid.BackgroundColor = SurfaceColor;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = BorderColor;
        grid.EnableHeadersVisualStyles = false;

        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = PrimaryTextColor;

        grid.DefaultCellStyle.BackColor = SurfaceColor;
        grid.DefaultCellStyle.ForeColor = PrimaryTextColor;
        grid.DefaultCellStyle.SelectionBackColor = SelectionAccentColor;
        grid.DefaultCellStyle.SelectionForeColor = PrimaryTextColor;

        grid.AlternatingRowsDefaultCellStyle.BackColor = SurfaceAltColor;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = PrimaryTextColor;
    }

    private sealed class LightMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => BorderColor;
        public override Color MenuItemBorder => BorderColor;
        public override Color MenuItemSelected => SelectionAccentColor;
        public override Color MenuItemSelectedGradientBegin => SelectionAccentColor;
        public override Color MenuItemSelectedGradientEnd => SelectionAccentColor;
        public override Color MenuStripGradientBegin => HeaderColor;
        public override Color MenuStripGradientEnd => HeaderColor;
        public override Color ToolStripDropDownBackground => SurfaceColor;
        public override Color ImageMarginGradientBegin => SurfaceColor;
        public override Color ImageMarginGradientMiddle => SurfaceColor;
        public override Color ImageMarginGradientEnd => SurfaceColor;
    }
}

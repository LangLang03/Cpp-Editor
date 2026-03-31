namespace C__Editor;

public partial class MainEditorForm
{
    private TabControl CreateEditorTabs()
    {
        var editorTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Name = "tabEditorHost",
            SelectedIndex = 0,
            TabIndex = 0
        };

        var editorPage = new TabPage
        {
            Name = "tabPageEditor",
            Text = "\u672A\u547D\u540D.cpp",
            Padding = new Padding(3),
            UseVisualStyleBackColor = true
        };

        Control editorHostControl = BuildEditorControlHost();
        editorPage.Controls.Add(editorHostControl);
        editorTabs.Controls.Add(editorPage);

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

            EditorThemeController.ApplyLightTheme(editorControlMain);
            InitializeSyntaxHighlighting();
            const string initialFileName = "untitled.cpp";
            var initialText = NormalizeEditorNewlines("// Ready\n");
            SetEditorSyntaxSource(initialFileName, initialText);
            editorControlMain.LoadDocument(new SweetEditor.Document(initialText));
            editorControlMain.RequestDecorationRefresh();
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
                Text = "\u7F16\u8F91\u5668\u52A0\u8F7D\u5931\u8D25\r\n" + ex.Message
            };

            return fallbackLabel;
        }
    }
}

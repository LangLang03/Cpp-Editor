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

        var placeholderPanel = new Panel
        {
            Name = "panelEditorPlaceholder",
            Dock = DockStyle.Fill
        };

        var placeholderLabel = new Label
        {
            Name = "lblEditorPlaceholder",
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 13.8F, FontStyle.Regular, GraphicsUnit.Point, 134),
            ForeColor = SystemColors.ControlDarkDark,
            Text = "\u4EE3\u7801\u7F16\u8F91\u533A\u57DF\uFF08\u5F85\u5B9E\u73B0\uFF09",
            TextAlign = ContentAlignment.MiddleCenter
        };

        placeholderPanel.Controls.Add(placeholderLabel);
        editorPage.Controls.Add(placeholderPanel);
        editorTabs.Controls.Add(editorPage);

        return editorTabs;
    }
}

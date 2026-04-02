namespace C__Editor;

internal static class TextInputDialog
{
    internal static string? Show(IWin32Window owner, string title, string prompt, string defaultValue)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(460, 132)
        };

        var label = new Label
        {
            AutoSize = false,
            Left = 12,
            Top = 12,
            Width = 436,
            Height = 24,
            Text = prompt
        };

        var textBox = new TextBox
        {
            Left = 12,
            Top = 42,
            Width = 436,
            Text = defaultValue ?? string.Empty
        };

        var buttonOk = new Button
        {
            Text = "确定",
            Left = 292,
            Top = 82,
            Width = 74,
            DialogResult = DialogResult.OK
        };

        var buttonCancel = new Button
        {
            Text = "取消",
            Left = 374,
            Top = 82,
            Width = 74,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(buttonOk);
        dialog.Controls.Add(buttonCancel);
        dialog.AcceptButton = buttonOk;
        dialog.CancelButton = buttonCancel;

        var themeId = EditorConfigurationController.GetUiSettings().ThemeId;
        EditorThemeController.ApplyFlatTheme(themeId, dialog);

        var result = dialog.ShowDialog(owner);
        return result == DialogResult.OK ? textBox.Text : null;
    }
}

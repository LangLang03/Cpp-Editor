namespace C__Editor;

public partial class MainEditorForm
{
    private StatusStrip CreateEditorStatusBar()
    {
        statusEditorSpacer = new ToolStripStatusLabel
        {
            Name = "statusEditorSpacer",
            Spring = true,
            Text = string.Empty
        };

        statusEditorInfo = new ToolStripStatusLabel
        {
            Name = "statusEditorInfo",
            TextAlign = ContentAlignment.MiddleRight
        };

        var statusBar = new StatusStrip
        {
            Name = "statusEditor",
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };

        statusBar.Items.Add(statusEditorSpacer);
        statusBar.Items.Add(statusEditorInfo);
        return statusBar;
    }

    private void UpdateEditorStatusBar(SweetEditor.TextPosition? cursorOverride = null)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateEditorStatusBar(cursorOverride)));
            return;
        }

        if (statusEditorInfo is null)
        {
            return;
        }

        var state = GetSelectedDocumentState();
        var fileName = state is null
            ? "-"
            : ResolveDocumentDisplayName(state.FilePath, state.DisplayName);

        var encodingName = state?.EncodingDisplayName;
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            encodingName = EditorFileEncodingHelper.GetDisplayName(state?.TextEncoding ?? EditorFileEncodingHelper.DefaultEncoding);
        }

        var cursorPosition = ResolveStatusCursorPosition(cursorOverride);
        statusEditorInfo.Text = $"\u884C {cursorPosition.Line}, \u5217 {cursorPosition.Column} | {fileName} | {encodingName}";
    }

    private (int Line, int Column) ResolveStatusCursorPosition(SweetEditor.TextPosition? cursorOverride)
    {
        if (cursorOverride is SweetEditor.TextPosition cursor)
        {
            return (Math.Max(0, cursor.Line) + 1, Math.Max(0, cursor.Column) + 1);
        }

        if (editorControlMain is null)
        {
            return (1, 1);
        }

        try
        {
            var currentCursor = editorControlMain.GetCursorPosition();
            return (Math.Max(0, currentCursor.Line) + 1, Math.Max(0, currentCursor.Column) + 1);
        }
        catch
        {
            return (1, 1);
        }
    }
}

namespace C__Editor;

public partial class MainEditorForm
{
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var normalizedKeyData = EditorShortcutKeyFormatter.Normalize(keyData);
        if (TryHandleEditorClipboardShortcut(normalizedKeyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool TryHandleEditorClipboardShortcut(Keys normalizedKeyData)
    {
        if (editorControlMain is null || !editorControlMain.ContainsFocus)
        {
            return false;
        }

        if (normalizedKeyData == GetShortcutKey(EditorCommandIds.EditCopy))
        {
            CopyInEditor();
            return true;
        }

        if (normalizedKeyData == GetShortcutKey(EditorCommandIds.EditCut))
        {
            CutInEditor();
            return true;
        }

        if (normalizedKeyData == GetShortcutKey(EditorCommandIds.EditPaste))
        {
            PasteInEditor();
            return true;
        }

        return false;
    }
}

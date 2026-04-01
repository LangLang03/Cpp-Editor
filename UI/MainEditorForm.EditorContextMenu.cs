namespace C__Editor;

public partial class MainEditorForm
{
    private ContextMenuStrip editorContextMenu = null!;
    private ToolStripMenuItem menuEditorUndo = null!;
    private ToolStripMenuItem menuEditorRedo = null!;
    private ToolStripMenuItem menuEditorCut = null!;
    private ToolStripMenuItem menuEditorCopy = null!;
    private ToolStripMenuItem menuEditorPaste = null!;
    private ToolStripMenuItem menuEditorDelete = null!;
    private ToolStripMenuItem menuEditorSelectAll = null!;

    private void InitializeEditorContextMenu()
    {
        editorContextMenu?.Dispose();
        editorContextMenu = CreateEditorContextMenu();
    }

    private ContextMenuStrip CreateEditorContextMenu()
    {
        var menu = new ContextMenuStrip();

        menuEditorUndo = new ToolStripMenuItem("撤销");
        menuEditorUndo.Click += (_, _) => UndoInEditor();

        menuEditorRedo = new ToolStripMenuItem("重做");
        menuEditorRedo.Click += (_, _) => RedoInEditor();

        menuEditorCut = new ToolStripMenuItem("剪切");
        menuEditorCut.Click += (_, _) => CutInEditor();

        menuEditorCopy = new ToolStripMenuItem("复制");
        menuEditorCopy.Click += (_, _) => CopyInEditor();

        menuEditorPaste = new ToolStripMenuItem("粘贴");
        menuEditorPaste.Click += (_, _) => PasteInEditor();

        menuEditorDelete = new ToolStripMenuItem("删除");
        menuEditorDelete.Click += (_, _) => DeleteSelectionInEditor();

        menuEditorSelectAll = new ToolStripMenuItem("全选");
        menuEditorSelectAll.Click += (_, _) => SelectAllInEditor();

        menu.Opening += (_, _) => UpdateEditorContextMenuState();
        menu.Items.AddRange(
        [
            menuEditorUndo,
            menuEditorRedo,
            new ToolStripSeparator(),
            menuEditorCut,
            menuEditorCopy,
            menuEditorPaste,
            menuEditorDelete,
            new ToolStripSeparator(),
            menuEditorSelectAll
        ]);

        return menu;
    }

    private void ShowEditorContextMenu(PointF point)
    {
        if (editorControlMain is null || editorContextMenu is null)
        {
            return;
        }

        var location = new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        if (location.X < 0 ||
            location.Y < 0 ||
            location.X > editorControlMain.Width ||
            location.Y > editorControlMain.Height)
        {
            location = editorControlMain.PointToClient(Cursor.Position);
        }

        UpdateEditorContextMenuState();
        editorContextMenu.Show(editorControlMain, location);
    }

    private void UpdateEditorContextMenuState()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var selection = editorControlMain.GetSelection();
        var hasSelection = selection.hasSelection;

        menuEditorUndo.Enabled = editorControlMain.CanUndo();
        menuEditorRedo.Enabled = editorControlMain.CanRedo();
        menuEditorCut.Enabled = hasSelection;
        menuEditorCopy.Enabled = hasSelection;
        menuEditorDelete.Enabled = hasSelection;
        menuEditorPaste.Enabled = !string.IsNullOrEmpty(GetClipboardTextPreferUnicode());
        menuEditorSelectAll.Enabled = HasEditorContent();
    }

    private void DeleteSelectionInEditor()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var selection = editorControlMain.GetSelection();
        if (!selection.hasSelection)
        {
            return;
        }

        editorControlMain.DeleteText(selection.range);
        editorControlMain.Focus();
    }

    private bool HasEditorContent()
    {
        if (editorControlMain is null)
        {
            return false;
        }

        try
        {
            var document = editorControlMain.GetDocument();
            if (document is null)
            {
                return false;
            }

            var lineCount = document.GetLineCount();
            if (lineCount <= 0)
            {
                return false;
            }

            if (lineCount > 1)
            {
                return true;
            }

            return !string.IsNullOrEmpty(document.GetLineText(0));
        }
        catch
        {
            return false;
        }
    }
}

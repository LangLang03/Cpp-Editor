namespace C__Editor;

public partial class MainEditorForm
{
    private readonly Dictionary<string, List<ToolStripMenuItem>> menuShortcutItems = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, Keys> shortcutBindings = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);

    private void RegisterMenuShortcut(string commandId, ToolStripMenuItem menuItem)
    {
        if (!menuShortcutItems.TryGetValue(commandId, out var items))
        {
            items = new List<ToolStripMenuItem>();
            menuShortcutItems[commandId] = items;
        }

        items.Add(menuItem);
    }

    private void ReloadShortcutBindings()
    {
        shortcutBindings = EditorShortcutController.GetBindings();
        ApplyMenuShortcutBindings();
        ApplyProjectTreeShortcutDisplayStrings();
    }

    private IReadOnlyList<ShortcutBindingItem> GetShortcutBindingsForEditing()
    {
        return EditorShortcutController.GetEditableBindings();
    }

    private void SaveShortcutBindingsFromSettings(IReadOnlyList<ShortcutBindingItem> bindings)
    {
        EditorShortcutController.SaveEditableBindings(bindings);
        ReloadShortcutBindings();
    }

    private void ApplyMenuShortcutBindings()
    {
        foreach (var pair in menuShortcutItems)
        {
            var shortcut = GetShortcutKey(pair.Key);
            foreach (var menuItem in pair.Value)
            {
                menuItem.ShortcutKeys = shortcut;
                menuItem.ShowShortcutKeys = shortcut != Keys.None;
            }
        }
    }

    private Keys GetShortcutKey(string commandId)
    {
        return shortcutBindings.TryGetValue(commandId, out var keys)
            ? EditorShortcutKeyFormatter.Normalize(keys)
            : Keys.None;
    }

    private string GetShortcutDisplayText(string commandId)
    {
        return EditorShortcutKeyFormatter.ToDisplayString(GetShortcutKey(commandId));
    }

    private bool IsShortcutTriggered(KeyEventArgs e, string commandId)
    {
        var configured = GetShortcutKey(commandId);
        if (configured == Keys.None)
        {
            return false;
        }

        var keyData = EditorShortcutKeyFormatter.Normalize(e.KeyData);
        return keyData == configured;
    }
}

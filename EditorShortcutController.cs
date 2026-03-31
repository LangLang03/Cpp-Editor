namespace C__Editor;

internal sealed class ShortcutBindingItem
{
    public string CommandId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string CommandName { get; set; } = string.Empty;

    public string Gesture { get; set; } = string.Empty;

    public string DefaultGesture { get; set; } = string.Empty;

    internal ShortcutBindingItem Clone()
    {
        return new ShortcutBindingItem
        {
            CommandId = CommandId,
            Category = Category,
            CommandName = CommandName,
            Gesture = Gesture,
            DefaultGesture = DefaultGesture
        };
    }
}

internal static class EditorShortcutController
{
    internal static IReadOnlyDictionary<string, Keys> GetBindings()
    {
        var gestures = EditorConfigurationController.GetShortcutGestures();
        var bindings = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in EditorShortcutCatalog.GetDefinitions())
        {
            var gesture = gestures.TryGetValue(definition.CommandId, out var configured)
                ? configured
                : definition.DefaultGesture;

            if (EditorShortcutKeyFormatter.TryParse(gesture, out var keys))
            {
                bindings[definition.CommandId] = keys;
            }
        }

        return bindings;
    }

    internal static IReadOnlyList<ShortcutBindingItem> GetEditableBindings()
    {
        var gestures = EditorConfigurationController.GetShortcutGestures();
        var result = new List<ShortcutBindingItem>();
        foreach (var definition in EditorShortcutCatalog.GetDefinitions())
        {
            gestures.TryGetValue(definition.CommandId, out var gesture);
            result.Add(new ShortcutBindingItem
            {
                CommandId = definition.CommandId,
                Category = definition.Category,
                CommandName = definition.DisplayName,
                Gesture = gesture ?? definition.DefaultGesture,
                DefaultGesture = definition.DefaultGesture
            });
        }

        return result;
    }

    internal static void SaveEditableBindings(IEnumerable<ShortcutBindingItem> bindings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.CommandId))
            {
                continue;
            }

            if (!EditorShortcutKeyFormatter.TryParse(binding.Gesture, out var keys))
            {
                continue;
            }

            map[binding.CommandId] = EditorShortcutKeyFormatter.ToDisplayString(keys);
        }

        EditorConfigurationController.SaveShortcutGestures(map);
    }
}

internal static class EditorShortcutKeyFormatter
{
    private static readonly IReadOnlyDictionary<string, Keys> KeyAliases = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
    {
        ["Backspace"] = Keys.Back,
        ["Bksp"] = Keys.Back,
        ["Tab"] = Keys.Tab,
        ["Enter"] = Keys.Enter,
        ["Return"] = Keys.Enter,
        ["Esc"] = Keys.Escape,
        ["Escape"] = Keys.Escape,
        ["Space"] = Keys.Space,
        ["PgUp"] = Keys.PageUp,
        ["PageUp"] = Keys.PageUp,
        ["PgDn"] = Keys.PageDown,
        ["PageDown"] = Keys.PageDown,
        ["Ins"] = Keys.Insert,
        ["Insert"] = Keys.Insert,
        ["Del"] = Keys.Delete,
        ["Delete"] = Keys.Delete,
        ["Home"] = Keys.Home,
        ["End"] = Keys.End,
        ["Left"] = Keys.Left,
        ["Right"] = Keys.Right,
        ["Up"] = Keys.Up,
        ["Down"] = Keys.Down,
        ["Comma"] = Keys.Oemcomma,
        [","] = Keys.Oemcomma,
        ["Period"] = Keys.OemPeriod,
        ["."] = Keys.OemPeriod,
        ["Minus"] = Keys.OemMinus,
        ["-"] = Keys.OemMinus,
        ["Plus"] = Keys.Oemplus,
        ["="] = Keys.Oemplus,
        ["Semicolon"] = Keys.OemSemicolon,
        [";"] = Keys.OemSemicolon,
        ["Quote"] = Keys.OemQuotes,
        ["'"] = Keys.OemQuotes,
        ["Slash"] = Keys.OemQuestion,
        ["/"] = Keys.OemQuestion,
        ["Backslash"] = Keys.OemPipe,
        ["\\"] = Keys.OemPipe,
        ["LBracket"] = Keys.OemOpenBrackets,
        ["["] = Keys.OemOpenBrackets,
        ["RBracket"] = Keys.OemCloseBrackets,
        ["]"] = Keys.OemCloseBrackets,
        ["Tilde"] = Keys.Oemtilde,
        ["`"] = Keys.Oemtilde
    };

    internal static bool TryParse(string? gesture, out Keys keys)
    {
        keys = Keys.None;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return true;
        }

        var normalized = gesture.Trim();
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("无", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("<none>", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = normalized
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var modifiers = Keys.None;
        var keyCode = Keys.None;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Control;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Shift;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Alt;
                continue;
            }

            if (keyCode != Keys.None || !TryParseKeyCode(part, out keyCode))
            {
                return false;
            }
        }

        if (keyCode == Keys.None)
        {
            return false;
        }

        keys = keyCode | modifiers;
        return true;
    }

    internal static string ToDisplayString(Keys keys)
    {
        var normalized = Normalize(keys);
        if (normalized == Keys.None)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (normalized.HasFlag(Keys.Control))
        {
            parts.Add("Ctrl");
        }

        if (normalized.HasFlag(Keys.Shift))
        {
            parts.Add("Shift");
        }

        if (normalized.HasFlag(Keys.Alt))
        {
            parts.Add("Alt");
        }

        parts.Add(ToKeyName(normalized & Keys.KeyCode));
        return string.Join("+", parts);
    }

    internal static Keys Normalize(Keys keys)
    {
        return (keys & Keys.KeyCode) | (keys & Keys.Modifiers);
    }

    private static bool TryParseKeyCode(string token, out Keys keyCode)
    {
        keyCode = Keys.None;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (KeyAliases.TryGetValue(token, out keyCode))
        {
            return true;
        }

        if (token.Length == 1)
        {
            var c = token[0];
            if (c is >= 'A' and <= 'Z')
            {
                keyCode = Keys.A + (c - 'A');
                return true;
            }

            if (c is >= 'a' and <= 'z')
            {
                keyCode = Keys.A + (c - 'a');
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                keyCode = Keys.D0 + (c - '0');
                return true;
            }
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[1..], out var functionIndex) &&
            functionIndex is >= 1 and <= 24)
        {
            keyCode = Keys.F1 + (functionIndex - 1);
            return true;
        }

        if (!Enum.TryParse(token, ignoreCase: true, out keyCode))
        {
            return false;
        }

        keyCode &= Keys.KeyCode;
        return keyCode != Keys.None;
    }

    private static string ToKeyName(Keys keyCode)
    {
        if (keyCode is >= Keys.A and <= Keys.Z)
        {
            return ((char)('A' + (keyCode - Keys.A))).ToString();
        }

        if (keyCode is >= Keys.D0 and <= Keys.D9)
        {
            return ((char)('0' + (keyCode - Keys.D0))).ToString();
        }

        if (keyCode is >= Keys.F1 and <= Keys.F24)
        {
            return $"F{(int)(keyCode - Keys.F1 + 1)}";
        }

        return keyCode switch
        {
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemQuestion => "/",
            Keys.OemPipe => "\\",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.Oemtilde => "`",
            _ => keyCode.ToString()
        };
    }
}

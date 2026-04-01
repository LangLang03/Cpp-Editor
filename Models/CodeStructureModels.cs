namespace C__Editor;

internal enum CodeElementType
{
    Namespace,
    Class,
    Struct,
    Enum,
    Function,
    Method,
    Constructor,
    Destructor,
    Variable,
    Field,
    Typedef,
    Using,
    Include,
    Macro,
    Template,
    AccessSection
}

internal sealed class CodeElement
{
    public string Name { get; set; } = string.Empty;
    public CodeElementType Type { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string AccessModifier { get; set; } = string.Empty;
    public List<CodeElement> Children { get; set; } = new();
    public CodeElement? Parent { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Signature) ? Name : Signature;

    public string TypeIcon => Type switch
    {
        CodeElementType.Namespace => "N",
        CodeElementType.Class => "C",
        CodeElementType.Struct => "S",
        CodeElementType.Enum => "E",
        CodeElementType.Function => "F",
        CodeElementType.Method => "M",
        CodeElementType.Constructor => "Ctor",
        CodeElementType.Destructor => "Dtor",
        CodeElementType.Variable => "V",
        CodeElementType.Field => "Field",
        CodeElementType.Typedef => "Typedef",
        CodeElementType.Using => "Using",
        CodeElementType.Include => "Inc",
        CodeElementType.Macro => "Macro",
        CodeElementType.Template => "Tpl",
        CodeElementType.AccessSection => "Access",
        _ => "?"
    };

    internal CodeElement Clone()
    {
        var clone = new CodeElement
        {
            Name = Name,
            Type = Type,
            LineNumber = LineNumber,
            ColumnNumber = ColumnNumber,
            Signature = Signature,
            AccessModifier = AccessModifier,
            Parent = Parent
        };

        foreach (var child in Children)
        {
            var childClone = child.Clone();
            childClone.Parent = clone;
            clone.Children.Add(childClone);
        }

        return clone;
    }
}

internal sealed class CodeStructureParseResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<CodeElement> Elements { get; set; } = new();
    public DateTime ParseTime { get; set; }
    public bool IsPartial { get; set; }
    public string? ErrorMessage { get; set; }

    internal CodeStructureParseResult Clone()
    {
        return new CodeStructureParseResult
        {
            FilePath = FilePath,
            Elements = Elements.Select(e => e.Clone()).ToList(),
            ParseTime = ParseTime,
            IsPartial = IsPartial,
            ErrorMessage = ErrorMessage
        };
    }
}

internal sealed class CodeStructureSettings
{
    public bool ShowIncludes { get; set; } = true;
    public bool ShowMacros { get; set; } = false;
    public bool ShowVariables { get; set; } = false;
    public bool SortAlphabetically { get; set; } = false;
    public bool AutoRefresh { get; set; } = true;

    internal static CodeStructureSettings CreateDefault()
    {
        return new CodeStructureSettings
        {
            ShowIncludes = true,
            ShowMacros = false,
            ShowVariables = false,
            SortAlphabetically = false,
            AutoRefresh = true
        };
    }

    internal CodeStructureSettings Clone()
    {
        return new CodeStructureSettings
        {
            ShowIncludes = ShowIncludes,
            ShowMacros = ShowMacros,
            ShowVariables = ShowVariables,
            SortAlphabetically = SortAlphabetically,
            AutoRefresh = AutoRefresh
        };
    }
}

namespace C__Editor;

internal enum SnippetCategory
{
    ControlFlow,
    Functions,
    Classes,
    Templates,
    Common,
    Custom
}

internal sealed class CodeSnippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public SnippetCategory Category { get; set; } = SnippetCategory.Common;
    public string Shortcut { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    internal CodeSnippet Clone()
    {
        return new CodeSnippet
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Code = Code,
            Category = Category,
            Shortcut = Shortcut,
            IsBuiltIn = IsBuiltIn
        };
    }
}

internal static class CodeSnippetCatalog
{
    private static readonly List<CodeSnippet> BuiltInSnippets = new()
    {
        // Control Flow
        new CodeSnippet
        {
            Name = "if",
            Description = "if statement",
            Code = "if (condition)\n{\n    \n}",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "if",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "if-else",
            Description = "if-else statement",
            Code = "if (condition)\n{\n    \n}\nelse\n{\n    \n}",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "ife",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "for loop",
            Description = "for loop",
            Code = "for (int i = 0; i < count; i++)\n{\n    \n}",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "for",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "while loop",
            Description = "while loop",
            Code = "while (condition)\n{\n    \n}",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "while",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "do-while",
            Description = "do-while loop",
            Code = "do\n{\n    \n} while (condition);",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "dow",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "switch",
            Description = "switch statement",
            Code = "switch (expression)\n{\n    case value:\n        break;\n    default:\n        break;\n}",
            Category = SnippetCategory.ControlFlow,
            Shortcut = "switch",
            IsBuiltIn = true
        },

        // Functions
        new CodeSnippet
        {
            Name = "main function",
            Description = "main function",
            Code = "int main()\n{\n    \n    return 0;\n}",
            Category = SnippetCategory.Functions,
            Shortcut = "main",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "function",
            Description = "function definition",
            Code = "returnType functionName(parameters)\n{\n    \n}",
            Category = SnippetCategory.Functions,
            Shortcut = "func",
            IsBuiltIn = true
        },

        // Classes
        new CodeSnippet
        {
            Name = "class",
            Description = "class definition",
            Code = "class ClassName\n{\npublic:\n    ClassName();\n    ~ClassName();\n\nprivate:\n    \n};",
            Category = SnippetCategory.Classes,
            Shortcut = "class",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "struct",
            Description = "struct definition",
            Code = "struct StructName\n{\n    \n};",
            Category = SnippetCategory.Classes,
            Shortcut = "struct",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "constructor",
            Description = "class constructor",
            Code = "ClassName::ClassName()\n{\n    \n}",
            Category = SnippetCategory.Classes,
            Shortcut = "ctor",
            IsBuiltIn = true
        },

        // Templates
        new CodeSnippet
        {
            Name = "template class",
            Description = "template class",
            Code = "template<typename T>\nclass ClassName\n{\npublic:\n    \n};",
            Category = SnippetCategory.Templates,
            Shortcut = "tmpl",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "template function",
            Description = "template function",
            Code = "template<typename T>\nvoid functionName(T param)\n{\n    \n}",
            Category = SnippetCategory.Templates,
            Shortcut = "tmpf",
            IsBuiltIn = true
        },

        // Common
        new CodeSnippet
        {
            Name = "cout",
            Description = "std::cout",
            Code = "std::cout << \"\" << std::endl;",
            Category = SnippetCategory.Common,
            Shortcut = "cout",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "cin",
            Description = "std::cin",
            Code = "std::cin >> variable;",
            Category = SnippetCategory.Common,
            Shortcut = "cin",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "vector",
            Description = "std::vector",
            Code = "std::vector<Type> name;",
            Category = SnippetCategory.Common,
            Shortcut = "vec",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "map",
            Description = "std::map",
            Code = "std::map<KeyType, ValueType> name;",
            Category = SnippetCategory.Common,
            Shortcut = "map",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "include",
            Description = "#include",
            Code = "#include <>",
            Category = SnippetCategory.Common,
            Shortcut = "inc",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "namespace",
            Description = "namespace",
            Code = "namespace Name\n{\n    \n}",
            Category = SnippetCategory.Common,
            Shortcut = "ns",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "try-catch",
            Description = "try-catch block",
            Code = "try\n{\n    \n}\ncatch (const std::exception& e)\n{\n    \n}",
            Category = SnippetCategory.Common,
            Shortcut = "try",
            IsBuiltIn = true
        },
        new CodeSnippet
        {
            Name = "lambda",
            Description = "lambda expression",
            Code = "auto lambda = [](auto param) {\n    return param;\n};",
            Category = SnippetCategory.Common,
            Shortcut = "lambda",
            IsBuiltIn = true
        }
    };

    internal static IReadOnlyList<CodeSnippet> GetBuiltInSnippets()
    {
        return BuiltInSnippets.Select(s => s.Clone()).ToList();
    }

    internal static IReadOnlyList<CodeSnippet> GetSnippetsByCategory(SnippetCategory category)
    {
        return BuiltInSnippets
            .Where(s => s.Category == category)
            .Select(s => s.Clone())
            .ToList();
    }

    internal static CodeSnippet? FindByShortcut(string shortcut)
    {
        return BuiltInSnippets
            .FirstOrDefault(s => string.Equals(s.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase))
            ?.Clone();
    }
}

internal sealed class CodeSnippetSettings
{
    public List<CodeSnippet> CustomSnippets { get; set; } = new();
    public bool EnableShortcutExpansion { get; set; } = true;

    internal static CodeSnippetSettings CreateDefault()
    {
        return new CodeSnippetSettings
        {
            CustomSnippets = new List<CodeSnippet>(),
            EnableShortcutExpansion = true
        };
    }

    internal CodeSnippetSettings Clone()
    {
        return new CodeSnippetSettings
        {
            CustomSnippets = CustomSnippets?.Select(s => s.Clone()).ToList() ?? new List<CodeSnippet>(),
            EnableShortcutExpansion = EnableShortcutExpansion
        };
    }
}

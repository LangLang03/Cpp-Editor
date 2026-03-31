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
    Template
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
        CodeElementType.Namespace => "📦",
        CodeElementType.Class => "🟦",
        CodeElementType.Struct => "🟨",
        CodeElementType.Enum => "🔢",
        CodeElementType.Function => "🔧",
        CodeElementType.Method => "🔹",
        CodeElementType.Constructor => "🔸",
        CodeElementType.Destructor => "🔺",
        CodeElementType.Variable => "📄",
        CodeElementType.Field => "📋",
        CodeElementType.Typedef => "🏷️",
        CodeElementType.Using => "🔗",
        CodeElementType.Include => "📎",
        CodeElementType.Macro => "⚙️",
        CodeElementType.Template => "📐",
        _ => "•"
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

internal static class CodeStructureParser
{
    // Simple regex-based parser for C/C++ code structure
    private static readonly System.Text.RegularExpressions.Regex NamespaceRegex = new(
        @"namespace\s+(\w+)\s*\{", 
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex ClassRegex = new(
        @"(class|struct)\s+(\w+)(?:\s*:\s*(?:public|protected|private)\s+(\w+))?",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex FunctionRegex = new(
        @"(?:(?:static|inline|virtual|explicit|constexpr)\s+)*([\w:<>,\s*&]+?)\s+(\w+)\s*\(([^)]*)\)(?:\s*(?:const|override|final|noexcept)\s*)?(?:\s*\{|\s*;|\s*$)",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex ConstructorRegex = new(
        @"(\w+)\s*\(([^)]*)\)\s*(?::\s*\w+\s*\([^)]*\)\s*(?:,\s*\w+\s*\([^)]*\)\s*)*)?\s*\{",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex DestructorRegex = new(
        @"~\s*(\w+)\s*\(\s*\)",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex EnumRegex = new(
        @"enum\s+(?:class\s+)?(\w+)",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex VariableRegex = new(
        @"(?:(?:static|const|constexpr|mutable|extern)\s+)*(\w[\w:<>,\s*&]*)\s+(\w+)\s*(?:=\s*[^;]+)?;",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex IncludeRegex = new(
        @"#include\s+[<""]([^>""]+)[>""]",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex UsingRegex = new(
        @"using\s+(?:namespace\s+)?([\w:]+)",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex TypedefRegex = new(
        @"typedef\s+(\w[\w:<>,\s*&]*)\s+(\w+);",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex DefineRegex = new(
        @"#define\s+(\w+)(?:\s+(.+))?",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private static readonly System.Text.RegularExpressions.Regex TemplateRegex = new(
        @"template\s*<\s*typename\s+(\w+)\s*>",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static CodeStructureParseResult ParseFile(string filePath)
    {
        var result = new CodeStructureParseResult
        {
            FilePath = filePath,
            ParseTime = DateTime.Now
        };

        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }

            var content = File.ReadAllText(filePath);
            var lines = File.ReadAllLines(filePath);
            
            ParseContent(content, lines, result);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.IsPartial = true;
        }

        return result;
    }

    internal static CodeStructureParseResult ParseContent(string content, string[] lines, CodeStructureParseResult result)
    {
        var currentLine = 0;
        var inComment = false;
        var classStack = new Stack<CodeElement>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            currentLine = i + 1;
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            // Handle multi-line comments
            if (inComment)
            {
                if (line.Contains("*/"))
                    inComment = false;
                continue;
            }
            
            if (line.Contains("/*"))
            {
                if (!line.Contains("*/"))
                    inComment = true;
                continue;
            }
            
            // Skip single-line comments
            var codeOnly = line.Split(new[] { "//" }, StringSplitOptions.None)[0];
            if (string.IsNullOrWhiteSpace(codeOnly))
                continue;

            // Parse #include
            var includeMatch = IncludeRegex.Match(codeOnly);
            if (includeMatch.Success)
            {
                result.Elements.Add(new CodeElement
                {
                    Name = includeMatch.Groups[1].Value,
                    Type = CodeElementType.Include,
                    LineNumber = currentLine,
                    Signature = $"#include <{includeMatch.Groups[1].Value}>"
                });
                continue;
            }

            // Parse #define
            var defineMatch = DefineRegex.Match(codeOnly);
            if (defineMatch.Success)
            {
                result.Elements.Add(new CodeElement
                {
                    Name = defineMatch.Groups[1].Value,
                    Type = CodeElementType.Macro,
                    LineNumber = currentLine,
                    Signature = $"#define {defineMatch.Groups[1].Value} {defineMatch.Groups[2].Value}"
                });
                continue;
            }

            // Parse using
            var usingMatch = UsingRegex.Match(codeOnly);
            if (usingMatch.Success && !codeOnly.Contains("="))
            {
                result.Elements.Add(new CodeElement
                {
                    Name = usingMatch.Groups[1].Value,
                    Type = CodeElementType.Using,
                    LineNumber = currentLine,
                    Signature = $"using {usingMatch.Groups[1].Value}"
                });
                continue;
            }

            // Parse typedef
            var typedefMatch = TypedefRegex.Match(codeOnly);
            if (typedefMatch.Success)
            {
                result.Elements.Add(new CodeElement
                {
                    Name = typedefMatch.Groups[2].Value,
                    Type = CodeElementType.Typedef,
                    LineNumber = currentLine,
                    Signature = $"typedef {typedefMatch.Groups[1].Value} {typedefMatch.Groups[2].Value}"
                });
                continue;
            }

            // Parse namespace
            var namespaceMatch = NamespaceRegex.Match(codeOnly);
            if (namespaceMatch.Success)
            {
                var ns = new CodeElement
                {
                    Name = namespaceMatch.Groups[1].Value,
                    Type = CodeElementType.Namespace,
                    LineNumber = currentLine,
                    Signature = $"namespace {namespaceMatch.Groups[1].Value}"
                };
                result.Elements.Add(ns);
                classStack.Push(ns);
                continue;
            }

            // Parse template
            var templateMatch = TemplateRegex.Match(codeOnly);
            if (templateMatch.Success)
            {
                // Template is a modifier, mark it for next element
                continue;
            }

            // Parse class/struct
            var classMatch = ClassRegex.Match(codeOnly);
            if (classMatch.Success && codeOnly.Contains("{"))
            {
                var isStruct = classMatch.Groups[1].Value == "struct";
                var cls = new CodeElement
                {
                    Name = classMatch.Groups[2].Value,
                    Type = isStruct ? CodeElementType.Struct : CodeElementType.Class,
                    LineNumber = currentLine,
                    Signature = $"{classMatch.Groups[1].Value} {classMatch.Groups[2].Value}"
                };
                
                if (classStack.Count > 0)
                {
                    cls.Parent = classStack.Peek();
                    classStack.Peek().Children.Add(cls);
                }
                else
                {
                    result.Elements.Add(cls);
                }
                
                classStack.Push(cls);
                continue;
            }

            // Parse enum
            var enumMatch = EnumRegex.Match(codeOnly);
            if (enumMatch.Success)
            {
                var enm = new CodeElement
                {
                    Name = enumMatch.Groups[1].Value,
                    Type = CodeElementType.Enum,
                    LineNumber = currentLine,
                    Signature = $"enum {enumMatch.Groups[1].Value}"
                };
                
                if (classStack.Count > 0)
                {
                    enm.Parent = classStack.Peek();
                    classStack.Peek().Children.Add(enm);
                }
                else
                {
                    result.Elements.Add(enm);
                }
                continue;
            }

            // Parse destructor
            var dtorMatch = DestructorRegex.Match(codeOnly);
            if (dtorMatch.Success)
            {
                var dtor = new CodeElement
                {
                    Name = $"~{dtorMatch.Groups[1].Value}",
                    Type = CodeElementType.Destructor,
                    LineNumber = currentLine,
                    Signature = $"~{dtorMatch.Groups[1].Value}()"
                };
                
                if (classStack.Count > 0)
                {
                    dtor.Parent = classStack.Peek();
                    classStack.Peek().Children.Add(dtor);
                }
                continue;
            }

            // Parse function/method
            var funcMatch = FunctionRegex.Match(codeOnly);
            if (funcMatch.Success)
            {
                var returnType = funcMatch.Groups[1].Value.Trim();
                var funcName = funcMatch.Groups[2].Value;
                var parameters = funcMatch.Groups[3].Value;
                
                var func = new CodeElement
                {
                    Name = funcName,
                    Type = classStack.Count > 0 && classStack.Peek().Type is CodeElementType.Class or CodeElementType.Struct 
                        ? CodeElementType.Method 
                        : CodeElementType.Function,
                    LineNumber = currentLine,
                    Signature = $"{returnType} {funcName}({parameters})"
                };
                
                if (classStack.Count > 0)
                {
                    func.Parent = classStack.Peek();
                    classStack.Peek().Children.Add(func);
                }
                else
                {
                    result.Elements.Add(func);
                }
                continue;
            }

            // Track braces to manage class stack
            var openBraces = codeOnly.Count(c => c == '{');
            var closeBraces = codeOnly.Count(c => c == '}');
            
            if (closeBraces > openBraces)
            {
                var diff = closeBraces - openBraces;
                while (diff-- > 0 && classStack.Count > 0)
                {
                    classStack.Pop();
                }
            }
        }

        return result;
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

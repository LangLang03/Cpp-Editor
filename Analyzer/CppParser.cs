using System.Text;

namespace C__Editor;

internal sealed class CppParser
{
    private sealed class ClassScopeContext
    {
        private readonly Dictionary<string, CodeElement> accessSections = new(StringComparer.Ordinal);

        internal ClassScopeContext(CodeElement classElement, string qualifiedName, string defaultAccess)
        {
            ClassElement = classElement;
            QualifiedName = qualifiedName;
            DefaultAccess = defaultAccess;
            CurrentAccess = defaultAccess;
        }

        internal CodeElement ClassElement { get; }

        internal string QualifiedName { get; }

        internal string DefaultAccess { get; }

        internal string CurrentAccess { get; set; }

        internal CodeElement GetOrCreateAccessSection(string access, int line, int column)
        {
            if (accessSections.TryGetValue(access, out var existing))
            {
                return existing;
            }

            var section = new CodeElement
            {
                Name = access,
                Type = CodeElementType.AccessSection,
                LineNumber = line,
                ColumnNumber = column,
                Signature = $"{access}:"
            };

            section.Parent = ClassElement;
            ClassElement.Children.Add(section);
            accessSections[access] = section;
            return section;
        }
    }

    private readonly IReadOnlyList<CppToken> tokens;
    private readonly string filePath;
    private readonly Dictionary<string, CodeElement> classesByQualifiedName = new(StringComparer.Ordinal);
    private readonly List<string> parseErrors = new();
    private int position;
    private bool isPartial;

    internal CppParser(string filePath, IReadOnlyList<CppToken> tokens)
    {
        this.filePath = filePath;
        this.tokens = tokens;
    }

    internal CodeStructureParseResult Parse()
    {
        var result = new CodeStructureParseResult
        {
            FilePath = filePath,
            ParseTime = DateTime.Now
        };

        try
        {
            ParseScope(result.Elements, scopeOwner: null, qualifiedPrefix: string.Empty, classScope: null, stopAtRightBrace: false);
        }
        catch (Exception ex)
        {
            isPartial = true;
            parseErrors.Add(ex.Message);
        }

        result.IsPartial = isPartial;
        if (parseErrors.Count > 0)
        {
            result.ErrorMessage = string.Join(" | ", parseErrors);
        }

        return result;
    }

    private void ParseScope(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        string qualifiedPrefix,
        ClassScopeContext? classScope,
        bool stopAtRightBrace)
    {
        var pendingTemplatePrefix = string.Empty;

        while (!IsEnd)
        {
            if (stopAtRightBrace && MatchSymbol("}"))
            {
                return;
            }

            if (Current.Kind == CppTokenKind.Preprocessor)
            {
                ParsePreprocessor(elements, scopeOwner, classScope, pendingTemplatePrefix);
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseTemplateDeclaration(out var templatePrefix))
            {
                pendingTemplatePrefix = AppendPrefix(pendingTemplatePrefix, templatePrefix);
                continue;
            }

            if (classScope is not null && TryParseAccessLabel(classScope))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (classScope is null && TryParseNamespace(elements, scopeOwner, qualifiedPrefix, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (classScope is null && TryParseExternLinkageBlock(elements, scopeOwner, qualifiedPrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseClassOrStruct(elements, scopeOwner, qualifiedPrefix, classScope, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseEnum(elements, scopeOwner, classScope, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseUsing(elements, scopeOwner, classScope, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseTypedef(elements, scopeOwner, classScope, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            if (TryParseGeneralDeclaration(elements, scopeOwner, classScope, pendingTemplatePrefix))
            {
                pendingTemplatePrefix = string.Empty;
                continue;
            }

            isPartial = true;
            parseErrors.Add($"Unexpected token '{Current.Text}' at {Current.Line}:{Current.Column}");
            pendingTemplatePrefix = string.Empty;
            RecoverToNextBoundary(stopAtRightBrace);
        }
    }

    private bool TryParseTemplateDeclaration(out string templatePrefix)
    {
        templatePrefix = string.Empty;
        if (!CheckKeyword("template"))
        {
            return false;
        }

        var templateTokens = new List<CppToken>
        {
            Consume()
        };

        if (!CheckSymbol("<"))
        {
            templatePrefix = FormatTokens(templateTokens);
            return true;
        }

        var angleDepth = 0;
        while (!IsEnd)
        {
            var token = Consume();
            templateTokens.Add(token);

            if (token.IsSymbol("<"))
            {
                angleDepth++;
            }
            else if (token.IsSymbol(">"))
            {
                angleDepth--;
                if (angleDepth <= 0)
                {
                    break;
                }
            }
            else if (token.IsSymbol(">>"))
            {
                angleDepth -= 2;
                if (angleDepth <= 0)
                {
                    break;
                }
            }
        }

        templatePrefix = FormatTokens(templateTokens);
        return true;
    }

    private bool TryParseExternLinkageBlock(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        string qualifiedPrefix)
    {
        if (!CheckKeyword("extern"))
        {
            return false;
        }

        if (position + 2 >= tokens.Count)
        {
            return false;
        }

        var linkageToken = tokens[position + 1];
        var openBraceToken = tokens[position + 2];
        if (linkageToken.Kind != CppTokenKind.StringLiteral || !openBraceToken.IsSymbol("{"))
        {
            return false;
        }

        _ = Consume(); // extern
        _ = Consume(); // "C" / "C++"
        _ = Consume(); // {

        ParseScope(elements, scopeOwner, qualifiedPrefix, classScope: null, stopAtRightBrace: true);
        _ = MatchSymbol(";");
        return true;
    }

    private void ParsePreprocessor(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        var directive = Consume();
        if (directive.Kind != CppTokenKind.Preprocessor)
        {
            return;
        }

        if (string.Equals(directive.Text, "include", StringComparison.Ordinal))
        {
            var includeTarget = ExtractIncludeTarget(directive.Value);
            var signature = string.IsNullOrWhiteSpace(directive.Value)
                ? "#include"
                : $"#include {directive.Value}";

            if (!string.IsNullOrWhiteSpace(templatePrefix))
            {
                signature = $"{templatePrefix} {signature}";
            }

            var includeElement = new CodeElement
            {
                Name = includeTarget,
                Type = CodeElementType.Include,
                LineNumber = directive.Line,
                ColumnNumber = directive.Column,
                Signature = signature
            };

            AddElementToScope(elements, scopeOwner, classScope, includeElement);
            return;
        }

        if (string.Equals(directive.Text, "define", StringComparison.Ordinal))
        {
            var macroName = ExtractMacroName(directive.Value);
            var signature = string.IsNullOrWhiteSpace(directive.Value)
                ? "#define"
                : $"#define {directive.Value}";

            if (!string.IsNullOrWhiteSpace(templatePrefix))
            {
                signature = $"{templatePrefix} {signature}";
            }

            var macroElement = new CodeElement
            {
                Name = macroName,
                Type = CodeElementType.Macro,
                LineNumber = directive.Line,
                ColumnNumber = directive.Column,
                Signature = signature
            };

            AddElementToScope(elements, scopeOwner, classScope, macroElement);
        }
    }

    private bool TryParseNamespace(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        string qualifiedPrefix,
        string templatePrefix)
    {
        if (!CheckKeyword("namespace"))
        {
            return false;
        }

        var start = Consume();
        _ = MatchKeyword("inline");

        var parts = new List<string>();
        if (Current.Kind == CppTokenKind.Identifier)
        {
            parts.Add(Consume().Text);
            while (MatchSymbol("::") && Current.Kind == CppTokenKind.Identifier)
            {
                parts.Add(Consume().Text);
            }
        }
        else if (CheckSymbol("{"))
        {
            parts.Add("(anonymous)");
        }

        if (parts.Count == 0)
        {
            isPartial = true;
            parseErrors.Add($"Invalid namespace declaration at {start.Line}:{start.Column}");
            RecoverToNextBoundary(stopAtRightBrace: false);
            return true;
        }

        if (MatchSymbol("="))
        {
            // namespace alias: treat as using entry.
            var aliasTokens = new List<CppToken> { start };
            aliasTokens.Add(new CppToken(CppTokenKind.Identifier, string.Join("::", parts), start.Line, start.Column));
            aliasTokens.Add(new CppToken(CppTokenKind.Symbol, "=", start.Line, start.Column));
            while (!IsEnd && !CheckSymbol(";"))
            {
                aliasTokens.Add(Consume());
            }

            _ = MatchSymbol(";");

            var signature = FormatTokens(aliasTokens);
            if (!string.IsNullOrWhiteSpace(templatePrefix))
            {
                signature = $"{templatePrefix} {signature}";
            }

            var aliasElement = new CodeElement
            {
                Name = parts[^1],
                Type = CodeElementType.Using,
                LineNumber = start.Line,
                ColumnNumber = start.Column,
                Signature = signature
            };

            AddElementDirect(elements, scopeOwner, aliasElement);
            return true;
        }

        if (!MatchSymbol("{"))
        {
            isPartial = true;
            parseErrors.Add($"Namespace body expected at {start.Line}:{start.Column}");
            RecoverToNextBoundary(stopAtRightBrace: false);
            return true;
        }

        var currentElements = elements;
        var currentOwner = scopeOwner;
        var currentQualified = qualifiedPrefix;

        for (var i = 0; i < parts.Count; i++)
        {
            var name = parts[i];
            var ns = new CodeElement
            {
                Name = name,
                Type = CodeElementType.Namespace,
                LineNumber = start.Line,
                ColumnNumber = start.Column,
                Signature = string.IsNullOrWhiteSpace(templatePrefix)
                    ? $"namespace {name}"
                    : $"{templatePrefix} namespace {name}"
            };

            AddElementDirect(currentElements, currentOwner, ns);
            currentElements = ns.Children;
            currentOwner = ns;
            currentQualified = CombineQualified(currentQualified, name);
        }

        ParseScope(currentElements, currentOwner, currentQualified, classScope: null, stopAtRightBrace: true);
        _ = MatchSymbol(";");
        return true;
    }

    private bool TryParseClassOrStruct(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        string qualifiedPrefix,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        var isClass = CheckKeyword("class");
        var isStruct = CheckKeyword("struct");
        if (!isClass && !isStruct)
        {
            return false;
        }

        var keyword = Consume();
        var kindText = keyword.Text;

        var className = Current.Kind == CppTokenKind.Identifier
            ? Consume().Text
            : $"(anonymous {kindText})";

        var classTemplateArgumentTokens = new List<CppToken>();
        if (CheckSymbol("<"))
        {
            CollectBalancedAngleTokens(classTemplateArgumentTokens);
        }

        var classHeadSuffixTokens = new List<CppToken>();
        while (!IsEnd && !CheckSymbol(":") && !CheckSymbol("{") && !CheckSymbol(";"))
        {
            classHeadSuffixTokens.Add(Consume());
        }

        var inheritanceTokens = new List<CppToken>();
        if (MatchSymbol(":"))
        {
            var parenDepth = 0;
            var bracketDepth = 0;
            var braceDepth = 0;
            var angleDepth = 0;

            while (!IsEnd)
            {
                if (parenDepth == 0 &&
                    bracketDepth == 0 &&
                    braceDepth == 0 &&
                    angleDepth == 0 &&
                    (CheckSymbol("{") || CheckSymbol(";")))
                {
                    break;
                }

                var token = Consume();
                inheritanceTokens.Add(token);
                UpdateDelimiterDepths(token, ref parenDepth, ref bracketDepth, ref braceDepth, ref angleDepth);
            }
        }

        var classDisplayName = className;
        if (classTemplateArgumentTokens.Count > 0)
        {
            classDisplayName += FormatTokens(classTemplateArgumentTokens);
        }

        var signatureBuilder = new StringBuilder($"{kindText} {classDisplayName}");
        if (classHeadSuffixTokens.Count > 0)
        {
            signatureBuilder.Append(' ');
            signatureBuilder.Append(FormatTokens(classHeadSuffixTokens));
        }

        if (inheritanceTokens.Count > 0)
        {
            signatureBuilder.Append(" : ");
            signatureBuilder.Append(FormatTokens(inheritanceTokens));
        }

        var signature = signatureBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        var classElement = new CodeElement
        {
            Name = className,
            Type = isStruct ? CodeElementType.Struct : CodeElementType.Class,
            LineNumber = keyword.Line,
            ColumnNumber = keyword.Column,
            Signature = signature
        };

        AddElementToScope(elements, scopeOwner, classScope, classElement);

        var classQualifiedName = NormalizeQualifiedName(CombineQualified(qualifiedPrefix, className));
        if (!className.StartsWith("(anonymous", StringComparison.Ordinal))
        {
            classesByQualifiedName[classQualifiedName] = classElement;
        }

        if (!MatchSymbol("{"))
        {
            _ = MatchSymbol(";");
            return true;
        }

        var defaultAccess = isStruct ? "public" : "private";
        var nextClassScope = new ClassScopeContext(classElement, classQualifiedName, defaultAccess);
        ParseScope(classElement.Children, classElement, classQualifiedName, nextClassScope, stopAtRightBrace: true);
        _ = MatchSymbol(";");
        return true;
    }

    private bool TryParseEnum(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        if (!CheckKeyword("enum"))
        {
            return false;
        }

        var enumToken = Consume();
        var enumKind = "enum";
        if (CheckKeyword("class") || CheckKeyword("struct"))
        {
            enumKind += $" {Consume().Text}";
        }

        var enumName = Current.Kind == CppTokenKind.Identifier
            ? Consume().Text
            : "(anonymous enum)";

        var signature = $"{enumKind} {enumName}";
        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        var enumElement = new CodeElement
        {
            Name = enumName,
            Type = CodeElementType.Enum,
            LineNumber = enumToken.Line,
            ColumnNumber = enumToken.Column,
            Signature = signature
        };

        AddElementToScope(elements, scopeOwner, classScope, enumElement);

        if (MatchSymbol("{"))
        {
            SkipBalancedBlockFromOpenBrace();
        }

        _ = MatchSymbol(";");
        return true;
    }

    private bool TryParseAccessLabel(ClassScopeContext classScope)
    {
        if (!(CheckKeyword("public") || CheckKeyword("protected") || CheckKeyword("private")))
        {
            return false;
        }

        var accessToken = Consume();
        if (!MatchSymbol(":"))
        {
            // Not an access label; try to recover by keeping default behavior.
            isPartial = true;
            parseErrors.Add($"Invalid access specifier at {accessToken.Line}:{accessToken.Column}");
            return true;
        }

        classScope.CurrentAccess = accessToken.Text;
        classScope.GetOrCreateAccessSection(accessToken.Text, accessToken.Line, accessToken.Column);
        return true;
    }

    private bool TryParseUsing(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        if (!CheckKeyword("using"))
        {
            return false;
        }

        var usingTokens = new List<CppToken>
        {
            Consume()
        };

        CollectUntilStatementTerminator(usingTokens);

        _ = MatchSymbol(";");

        var signature = FormatTokens(usingTokens);
        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        var name = ResolveUsingName(usingTokens);
        var usingElement = new CodeElement
        {
            Name = name,
            Type = CodeElementType.Using,
            LineNumber = usingTokens[0].Line,
            ColumnNumber = usingTokens[0].Column,
            Signature = signature
        };

        AddElementToScope(elements, scopeOwner, classScope, usingElement);
        return true;
    }

    private bool TryParseTypedef(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        if (!CheckKeyword("typedef"))
        {
            return false;
        }

        var typedefTokens = new List<CppToken>
        {
            Consume()
        };

        CollectUntilStatementTerminator(typedefTokens);

        _ = MatchSymbol(";");

        var signature = FormatTokens(typedefTokens);
        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        var aliasName = ResolveTypedefName(typedefTokens);
        var typedefElement = new CodeElement
        {
            Name = aliasName,
            Type = CodeElementType.Typedef,
            LineNumber = typedefTokens[0].Line,
            ColumnNumber = typedefTokens[0].Column,
            Signature = signature
        };

        AddElementToScope(elements, scopeOwner, classScope, typedefElement);
        return true;
    }

    private bool TryParseGeneralDeclaration(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        string templatePrefix)
    {
        if (Current.Kind == CppTokenKind.EndOfFile || Current.IsSymbol("}"))
        {
            return false;
        }

        var declarationTokens = CollectDeclarationTokens();
        if (declarationTokens.Count == 0)
        {
            return false;
        }

        if (TryBuildFunctionElement(declarationTokens, classScope, templatePrefix, out var functionElement, out var qualifiedClassName))
        {
            if (!string.IsNullOrWhiteSpace(qualifiedClassName) && classesByQualifiedName.TryGetValue(qualifiedClassName, out var ownerClass))
            {
                AddOutOfClassMember(ownerClass, functionElement);
            }
            else
            {
                AddElementToScope(elements, scopeOwner, classScope, functionElement);
            }

            return true;
        }

        if (TryBuildVariableElement(declarationTokens, classScope, templatePrefix, out var variableElement))
        {
            AddElementToScope(elements, scopeOwner, classScope, variableElement);
            return true;
        }

        return true;
    }

    private List<CppToken> CollectDeclarationTokens()
    {
        var declarationTokens = new List<CppToken>();
        var parenDepth = 0;
        var bracketDepth = 0;

        while (!IsEnd)
        {
            if (CheckSymbol("}") && parenDepth == 0 && bracketDepth == 0)
            {
                break;
            }

            if (CheckSymbol(";") && parenDepth == 0 && bracketDepth == 0)
            {
                Consume();
                break;
            }

            if (CheckSymbol("{") && parenDepth == 0 && bracketDepth == 0)
            {
                if (TryExtractFunctionIdentity(declarationTokens, out _, out _, out _, out _, out _))
                {
                    Consume();
                    SkipBalancedBlockFromOpenBrace();
                    break;
                }

                declarationTokens.Add(Consume());
                CaptureBraceInitializer(declarationTokens);
                continue;
            }

            var token = Consume();
            declarationTokens.Add(token);

            if (token.IsSymbol("("))
            {
                parenDepth++;
            }
            else if (token.IsSymbol(")") && parenDepth > 0)
            {
                parenDepth--;
            }
            else if (token.IsSymbol("["))
            {
                bracketDepth++;
            }
            else if (token.IsSymbol("]") && bracketDepth > 0)
            {
                bracketDepth--;
            }
        }

        return declarationTokens;
    }

    private void CaptureBraceInitializer(List<CppToken> declarationTokens)
    {
        var depth = 1;
        while (!IsEnd && depth > 0)
        {
            var token = Consume();
            declarationTokens.Add(token);

            if (token.IsSymbol("{"))
            {
                depth++;
            }
            else if (token.IsSymbol("}"))
            {
                depth--;
            }
        }
    }

    private bool TryBuildFunctionElement(
        IReadOnlyList<CppToken> declarationTokens,
        ClassScopeContext? classScope,
        string templatePrefix,
        out CodeElement element,
        out string qualifiedClassName)
    {
        element = new CodeElement();
        qualifiedClassName = string.Empty;

        if (!TryExtractFunctionIdentity(
                declarationTokens,
                out _,
                out var nameText,
                out var isDestructor,
                out var qualifiers,
                out var namePosition))
        {
            return false;
        }

        var type = CodeElementType.Function;
        if (classScope is not null)
        {
            if (isDestructor)
            {
                type = CodeElementType.Destructor;
            }
            else if (string.Equals(nameText, classScope.ClassElement.Name, StringComparison.Ordinal))
            {
                type = CodeElementType.Constructor;
            }
            else
            {
                type = CodeElementType.Method;
            }
        }
        else if (qualifiers.Count > 0)
        {
            qualifiedClassName = NormalizeQualifiedName(string.Join("::", qualifiers));
            if (!string.IsNullOrWhiteSpace(qualifiedClassName) && classesByQualifiedName.TryGetValue(qualifiedClassName, out var classElement))
            {
                if (isDestructor)
                {
                    type = CodeElementType.Destructor;
                }
                else if (string.Equals(nameText, classElement.Name, StringComparison.Ordinal))
                {
                    type = CodeElementType.Constructor;
                }
                else
                {
                    type = CodeElementType.Method;
                }
            }
            else
            {
                qualifiedClassName = string.Empty;
            }
        }

        var displayName = isDestructor ? $"~{nameText}" : nameText;
        var signature = FormatTokens(declarationTokens);
        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        element = new CodeElement
        {
            Name = displayName,
            Type = type,
            LineNumber = namePosition.Line,
            ColumnNumber = namePosition.Column,
            Signature = signature,
            AccessModifier = classScope?.CurrentAccess ?? string.Empty
        };

        return true;
    }

    private bool TryBuildVariableElement(
        IReadOnlyList<CppToken> declarationTokens,
        ClassScopeContext? classScope,
        string templatePrefix,
        out CodeElement element)
    {
        element = new CodeElement();

        if (declarationTokens.Count == 0)
        {
            return false;
        }

        var first = declarationTokens[0];
        if (first.IsKeyword("return") ||
            first.IsKeyword("if") ||
            first.IsKeyword("for") ||
            first.IsKeyword("while") ||
            first.IsKeyword("switch") ||
            first.IsKeyword("catch"))
        {
            return false;
        }

        var hasParentheses = declarationTokens.Any(t => t.IsSymbol("("));
        if (hasParentheses)
        {
            return false;
        }

        CppToken? nameToken = null;
        for (var i = declarationTokens.Count - 1; i >= 0; i--)
        {
            var token = declarationTokens[i];
            if (token.Kind != CppTokenKind.Identifier)
            {
                continue;
            }

            if (i + 1 < declarationTokens.Count && declarationTokens[i + 1].IsSymbol("::"))
            {
                continue;
            }

            nameToken = token;
            break;
        }

        if (nameToken is null)
        {
            return false;
        }

        var signature = FormatTokens(declarationTokens);
        if (!string.IsNullOrWhiteSpace(templatePrefix))
        {
            signature = $"{templatePrefix} {signature}";
        }

        element = new CodeElement
        {
            Name = nameToken.Value.Text,
            Type = classScope is null ? CodeElementType.Variable : CodeElementType.Field,
            LineNumber = nameToken.Value.Line,
            ColumnNumber = nameToken.Value.Column,
            Signature = signature,
            AccessModifier = classScope?.CurrentAccess ?? string.Empty
        };

        return true;
    }

    private bool TryExtractFunctionIdentity(
        IReadOnlyList<CppToken> declarationTokens,
        out CppToken nameToken,
        out string nameText,
        out bool isDestructor,
        out List<string> qualifiers,
        out (int Line, int Column) namePosition)
    {
        nameToken = default;
        nameText = string.Empty;
        isDestructor = false;
        qualifiers = new List<string>();
        namePosition = (0, 0);

        var depth = 0;
        var openParen = -1;
        var closeParen = -1;

        for (var i = 0; i < declarationTokens.Count; i++)
        {
            var token = declarationTokens[i];
            if (token.IsSymbol("("))
            {
                if (depth == 0)
                {
                    openParen = i;
                }

                depth++;
            }
            else if (token.IsSymbol(")"))
            {
                if (depth > 0)
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeParen = i;
                    }
                }
            }
        }

        if (openParen <= 0 || closeParen <= openParen)
        {
            return false;
        }

        var index = openParen - 1;
        while (index >= 0 &&
               (declarationTokens[index].IsSymbol("&") ||
                declarationTokens[index].IsSymbol("*") ||
                declarationTokens[index].IsSymbol("&&")))
        {
            index--;
        }

        if (index < 0 || declarationTokens[index].Kind != CppTokenKind.Identifier)
        {
            return false;
        }

        nameToken = declarationTokens[index];
        nameText = nameToken.Text;

        var qualifierIndex = index - 1;
        if (index > 0 && declarationTokens[index - 1].IsSymbol("~"))
        {
            isDestructor = true;
            namePosition = (declarationTokens[index - 1].Line, declarationTokens[index - 1].Column);
            qualifierIndex = index - 2;
        }
        else
        {
            namePosition = (nameToken.Line, nameToken.Column);
        }

        while (qualifierIndex >= 0)
        {
            if (!declarationTokens[qualifierIndex].IsSymbol("::"))
            {
                break;
            }

            qualifierIndex--;
            if (qualifierIndex < 0)
            {
                break;
            }

            if (!TryReadQualifierComponentBackward(declarationTokens, ref qualifierIndex, out var qualifierName))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(qualifierName))
            {
                qualifiers.Insert(0, qualifierName);
            }
        }

        return true;
    }

    private static bool TryReadQualifierComponentBackward(
        IReadOnlyList<CppToken> declarationTokens,
        ref int index,
        out string qualifierName)
    {
        qualifierName = string.Empty;
        if (index < 0)
        {
            return false;
        }

        var cursor = index;
        if (declarationTokens[cursor].IsSymbol(">") || declarationTokens[cursor].IsSymbol(">>"))
        {
            var angleDepth = 0;
            while (cursor >= 0)
            {
                var token = declarationTokens[cursor];
                if (token.IsSymbol(">"))
                {
                    angleDepth++;
                }
                else if (token.IsSymbol(">>"))
                {
                    angleDepth += 2;
                }
                else if (token.IsSymbol("<"))
                {
                    angleDepth--;
                    if (angleDepth <= 0)
                    {
                        cursor--;
                        break;
                    }
                }

                cursor--;
            }
        }

        if (cursor < 0 || declarationTokens[cursor].Kind != CppTokenKind.Identifier)
        {
            return false;
        }

        qualifierName = declarationTokens[cursor].Text;
        cursor--;

        if (cursor >= 0 && declarationTokens[cursor].IsKeyword("template"))
        {
            cursor--;
        }

        index = cursor;
        return true;
    }

    private void CollectBalancedAngleTokens(List<CppToken> collectedTokens)
    {
        if (!CheckSymbol("<"))
        {
            return;
        }

        var angleDepth = 0;
        while (!IsEnd)
        {
            var token = Consume();
            collectedTokens.Add(token);

            if (token.IsSymbol("<"))
            {
                angleDepth++;
            }
            else if (token.IsSymbol(">"))
            {
                angleDepth--;
            }
            else if (token.IsSymbol(">>"))
            {
                angleDepth -= 2;
            }

            if (angleDepth <= 0)
            {
                break;
            }
        }
    }

    private void CollectUntilStatementTerminator(List<CppToken> collectedTokens)
    {
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var angleDepth = 0;

        while (!IsEnd)
        {
            if (parenDepth == 0 &&
                bracketDepth == 0 &&
                braceDepth == 0 &&
                angleDepth == 0 &&
                (CheckSymbol(";") || CheckSymbol("}")))
            {
                break;
            }

            var token = Consume();
            collectedTokens.Add(token);
            UpdateDelimiterDepths(token, ref parenDepth, ref bracketDepth, ref braceDepth, ref angleDepth);
        }
    }

    private static void UpdateDelimiterDepths(
        CppToken token,
        ref int parenDepth,
        ref int bracketDepth,
        ref int braceDepth,
        ref int angleDepth)
    {
        if (token.IsSymbol("("))
        {
            parenDepth++;
            return;
        }

        if (token.IsSymbol(")") && parenDepth > 0)
        {
            parenDepth--;
            return;
        }

        if (token.IsSymbol("["))
        {
            bracketDepth++;
            return;
        }

        if (token.IsSymbol("]") && bracketDepth > 0)
        {
            bracketDepth--;
            return;
        }

        if (token.IsSymbol("{"))
        {
            braceDepth++;
            return;
        }

        if (token.IsSymbol("}") && braceDepth > 0)
        {
            braceDepth--;
            return;
        }

        if (token.IsSymbol("<"))
        {
            angleDepth++;
            return;
        }

        if (token.IsSymbol(">") && angleDepth > 0)
        {
            angleDepth--;
            return;
        }

        if (token.IsSymbol(">>") && angleDepth > 0)
        {
            angleDepth = Math.Max(0, angleDepth - 2);
        }
    }

    private void AddOutOfClassMember(CodeElement classElement, CodeElement member)
    {
        var access = ResolveExistingAccessForMember(classElement, member.Name);
        if (string.IsNullOrWhiteSpace(access))
        {
            access = "public";
        }

        var section = FindAccessSection(classElement, access)
            ?? CreateAccessSection(classElement, access, member.LineNumber, member.ColumnNumber);

        member.AccessModifier = access;
        member.Parent = section;
        section.Children.Add(member);
    }

    private static string ResolveExistingAccessForMember(CodeElement classElement, string memberName)
    {
        foreach (var section in classElement.Children.Where(c => c.Type == CodeElementType.AccessSection))
        {
            if (section.Children.Any(child => string.Equals(child.Name, memberName, StringComparison.Ordinal)))
            {
                return section.Name;
            }
        }

        return string.Empty;
    }

    private static CodeElement? FindAccessSection(CodeElement classElement, string access)
    {
        return classElement.Children.FirstOrDefault(c =>
            c.Type == CodeElementType.AccessSection &&
            string.Equals(c.Name, access, StringComparison.Ordinal));
    }

    private static CodeElement CreateAccessSection(CodeElement classElement, string access, int line, int column)
    {
        var section = new CodeElement
        {
            Name = access,
            Type = CodeElementType.AccessSection,
            LineNumber = line,
            ColumnNumber = column,
            Signature = $"{access}:",
            Parent = classElement
        };

        classElement.Children.Add(section);
        return section;
    }

    private void AddElementToScope(
        List<CodeElement> elements,
        CodeElement? scopeOwner,
        ClassScopeContext? classScope,
        CodeElement element)
    {
        if (classScope is not null && IsAccessGroupedMember(element.Type))
        {
            var access = string.IsNullOrWhiteSpace(classScope.CurrentAccess)
                ? classScope.DefaultAccess
                : classScope.CurrentAccess;

            var accessSection = classScope.GetOrCreateAccessSection(access, element.LineNumber, element.ColumnNumber);
            element.AccessModifier = access;
            element.Parent = accessSection;
            accessSection.Children.Add(element);
            return;
        }

        AddElementDirect(elements, scopeOwner, element);
    }

    private static void AddElementDirect(List<CodeElement> elements, CodeElement? scopeOwner, CodeElement element)
    {
        element.Parent = scopeOwner;
        elements.Add(element);
    }

    private static bool IsAccessGroupedMember(CodeElementType type)
    {
        return type is CodeElementType.Method
            or CodeElementType.Constructor
            or CodeElementType.Destructor
            or CodeElementType.Field
            or CodeElementType.Variable
            or CodeElementType.Class
            or CodeElementType.Struct
            or CodeElementType.Enum
            or CodeElementType.Using
            or CodeElementType.Typedef
            or CodeElementType.Template;
    }

    private void SkipBalancedBlockFromOpenBrace()
    {
        var depth = 1;
        while (!IsEnd && depth > 0)
        {
            var token = Consume();
            if (token.IsSymbol("{"))
            {
                depth++;
            }
            else if (token.IsSymbol("}"))
            {
                depth--;
            }
        }
    }

    private void RecoverToNextBoundary(bool stopAtRightBrace)
    {
        if (IsEnd)
        {
            return;
        }

        if (MatchSymbol(";"))
        {
            return;
        }

        if (stopAtRightBrace && CheckSymbol("}"))
        {
            return;
        }

        if (MatchSymbol("{"))
        {
            SkipBalancedBlockFromOpenBrace();
            return;
        }

        while (!IsEnd)
        {
            if (MatchSymbol(";"))
            {
                return;
            }

            if (stopAtRightBrace && CheckSymbol("}"))
            {
                return;
            }

            if (MatchSymbol("{"))
            {
                SkipBalancedBlockFromOpenBrace();
                return;
            }

            Consume();
        }
    }

    private bool IsEnd => position >= tokens.Count || Current.Kind == CppTokenKind.EndOfFile;

    private CppToken Current => position < tokens.Count ? tokens[position] : tokens[^1];

    private CppToken Consume()
    {
        if (position >= tokens.Count)
        {
            return tokens[^1];
        }

        return tokens[position++];
    }

    private bool CheckSymbol(string symbol)
    {
        return !IsEnd && Current.IsSymbol(symbol);
    }

    private bool MatchSymbol(string symbol)
    {
        if (!CheckSymbol(symbol))
        {
            return false;
        }

        Consume();
        return true;
    }

    private bool CheckKeyword(string keyword)
    {
        return !IsEnd && Current.IsKeyword(keyword);
    }

    private bool MatchKeyword(string keyword)
    {
        if (!CheckKeyword(keyword))
        {
            return false;
        }

        Consume();
        return true;
    }

    private static string ResolveUsingName(IReadOnlyList<CppToken> usingTokens)
    {
        if (usingTokens.Count <= 1)
        {
            return "using";
        }

        if (usingTokens.Count > 2 && usingTokens[1].IsKeyword("namespace"))
        {
            for (var i = usingTokens.Count - 1; i >= 2; i--)
            {
                if (usingTokens[i].Kind == CppTokenKind.Identifier)
                {
                    return usingTokens[i].Text;
                }
            }
        }

        var equalsIndex = usingTokens.ToList().FindIndex(t => t.IsSymbol("="));
        if (equalsIndex > 1)
        {
            for (var i = equalsIndex - 1; i >= 1; i--)
            {
                if (usingTokens[i].Kind == CppTokenKind.Identifier)
                {
                    return usingTokens[i].Text;
                }
            }
        }

        for (var i = usingTokens.Count - 1; i >= 1; i--)
        {
            if (usingTokens[i].Kind == CppTokenKind.Identifier)
            {
                return usingTokens[i].Text;
            }
        }

        return "using";
    }

    private static string ResolveTypedefName(IReadOnlyList<CppToken> typedefTokens)
    {
        for (var i = typedefTokens.Count - 1; i >= 1; i--)
        {
            if (typedefTokens[i].Kind == CppTokenKind.Identifier)
            {
                return typedefTokens[i].Text;
            }
        }

        return "typedef";
    }

    private static string ExtractIncludeTarget(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "include";
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        return trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static string ExtractMacroName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "define";
        }

        var trimmed = value.Trim();
        var end = 0;
        while (end < trimmed.Length)
        {
            var ch = trimmed[end];
            if (ch == '_' || char.IsLetterOrDigit(ch))
            {
                end++;
                continue;
            }

            break;
        }

        return end > 0 ? trimmed[..end] : "define";
    }

    private static string CombineQualified(string prefix, string name)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return name;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return prefix;
        }

        return $"{prefix}::{name}";
    }

    private static string NormalizeQualifiedName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var angleDepth = 0;
        for (var i = 0; i < qualifiedName.Length; i++)
        {
            var ch = qualifiedName[i];
            if (ch == '<')
            {
                angleDepth++;
                continue;
            }

            if (ch == '>')
            {
                if (angleDepth > 0)
                {
                    angleDepth--;
                }

                continue;
            }

            if (angleDepth > 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        var normalized = builder.ToString();
        while (normalized.StartsWith("::", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private static string AppendPrefix(string prefix, string nextPrefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return nextPrefix;
        }

        if (string.IsNullOrWhiteSpace(nextPrefix))
        {
            return prefix;
        }

        return $"{prefix} {nextPrefix}";
    }

    private static string FormatTokens(IReadOnlyList<CppToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        CppToken? previous = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (builder.Length > 0 && NeedSpace(previous!.Value, token))
            {
                builder.Append(' ');
            }

            builder.Append(token.Text);
            previous = token;
        }

        return builder.ToString().Trim();
    }

    private static bool NeedSpace(CppToken left, CppToken right)
    {
        if (left.IsSymbol("(") || left.IsSymbol("[") || left.IsSymbol("{") || left.IsSymbol("::") || left.IsSymbol("~"))
        {
            return false;
        }

        if (right.IsSymbol(")") || right.IsSymbol("]") || right.IsSymbol("}") || right.IsSymbol(",") || right.IsSymbol(";"))
        {
            return false;
        }

        if (right.IsSymbol("::") || right.IsSymbol("("))
        {
            return false;
        }

        if (left.IsSymbol("::") || left.IsSymbol(","))
        {
            return true;
        }

        if (left.IsSymbol(".") || right.IsSymbol(".") || left.IsSymbol("->") || right.IsSymbol("->"))
        {
            return false;
        }

        return true;
    }
}

using System.Text;

namespace C__Editor;

internal sealed class CppLexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "alignas", "alignof", "asm", "auto", "bool", "break", "case", "catch", "char", "class",
        "const", "consteval", "constexpr", "constinit", "const_cast", "continue", "decltype", "default",
        "delete", "do", "double", "dynamic_cast", "else", "enum", "explicit", "export", "extern",
        "false", "final", "float", "for", "friend", "goto", "if", "inline", "int", "long", "mutable",
        "namespace", "new", "noexcept", "nullptr", "operator", "override", "private", "protected", "public",
        "register", "reinterpret_cast", "requires", "return", "short", "signed", "sizeof", "static",
        "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local", "throw",
        "true", "try", "typedef", "typename", "union", "unsigned", "using", "virtual", "void", "volatile",
        "wchar_t", "while"
    };

    private static readonly string[] MultiCharSymbols =
    {
        "::", "->", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||", "++", "--", "+=", "-=",
        "*=", "/=", "%=", "&=", "|=", "^=", "..."
    };

    private readonly string source;
    private int position;
    private int line = 1;
    private int column = 1;
    private bool lineHasOnlyWhitespace = true;

    internal CppLexer(string source)
    {
        this.source = source ?? string.Empty;
    }

    internal List<CppToken> Tokenize()
    {
        var tokens = new List<CppToken>();

        while (!IsAtEnd)
        {
            if (TryConsumeWhitespace())
            {
                continue;
            }

            if (TryConsumeComment())
            {
                continue;
            }

            if (Peek() == '#' && lineHasOnlyWhitespace)
            {
                tokens.Add(ReadPreprocessor());
                lineHasOnlyWhitespace = false;
                continue;
            }

            var ch = Peek();
            if (IsIdentifierStart(ch))
            {
                tokens.Add(ReadIdentifierOrKeyword());
                lineHasOnlyWhitespace = false;
                continue;
            }

            if (char.IsDigit(ch))
            {
                tokens.Add(ReadNumberLiteral());
                lineHasOnlyWhitespace = false;
                continue;
            }

            if (ch == '"')
            {
                tokens.Add(ReadQuotedLiteral('"', CppTokenKind.StringLiteral));
                lineHasOnlyWhitespace = false;
                continue;
            }

            if (ch == '\'')
            {
                tokens.Add(ReadQuotedLiteral('\'', CppTokenKind.CharLiteral));
                lineHasOnlyWhitespace = false;
                continue;
            }

            tokens.Add(ReadSymbol());
            lineHasOnlyWhitespace = false;
        }

        tokens.Add(new CppToken(CppTokenKind.EndOfFile, string.Empty, line, column));
        return tokens;
    }

    private bool IsAtEnd => position >= source.Length;

    private char Peek(int offset = 0)
    {
        var index = position + offset;
        return index >= source.Length ? '\0' : source[index];
    }

    private char Advance()
    {
        if (IsAtEnd)
        {
            return '\0';
        }

        var ch = source[position++];
        if (ch == '\r')
        {
            if (!IsAtEnd && source[position] == '\n')
            {
                position++;
            }

            line++;
            column = 1;
            lineHasOnlyWhitespace = true;
            return '\n';
        }

        if (ch == '\n')
        {
            line++;
            column = 1;
            lineHasOnlyWhitespace = true;
            return '\n';
        }

        column++;
        return ch;
    }

    private bool TryConsumeWhitespace()
    {
        var consumed = false;
        while (!IsAtEnd)
        {
            var ch = Peek();
            if (ch == ' ' || ch == '\t' || ch == '\f' || ch == '\v')
            {
                Advance();
                consumed = true;
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                Advance();
                consumed = true;
                continue;
            }

            break;
        }

        return consumed;
    }

    private bool TryConsumeComment()
    {
        if (Peek() == '/' && Peek(1) == '/')
        {
            Advance();
            Advance();
            while (!IsAtEnd)
            {
                var ch = Peek();
                if (ch == '\r' || ch == '\n')
                {
                    break;
                }

                Advance();
            }

            return true;
        }

        if (Peek() == '/' && Peek(1) == '*')
        {
            Advance();
            Advance();
            while (!IsAtEnd)
            {
                if (Peek() == '*' && Peek(1) == '/')
                {
                    Advance();
                    Advance();
                    break;
                }

                Advance();
            }

            return true;
        }

        return false;
    }

    private CppToken ReadPreprocessor()
    {
        var startLine = line;
        var startColumn = column;

        Advance(); // #
        SkipHorizontalWhitespace();

        var directiveBuilder = new StringBuilder();
        while (!IsAtEnd && IsIdentifierPart(Peek()))
        {
            directiveBuilder.Append(Advance());
        }

        SkipHorizontalWhitespace();

        var valueBuilder = new StringBuilder();
        while (!IsAtEnd)
        {
            var ch = Peek();
            if (ch == '\r' || ch == '\n')
            {
                break;
            }

            valueBuilder.Append(Advance());
        }

        var directive = directiveBuilder.ToString();
        return new CppToken(
            CppTokenKind.Preprocessor,
            directive,
            startLine,
            startColumn,
            valueBuilder.ToString().Trim());
    }

    private CppToken ReadIdentifierOrKeyword()
    {
        var startLine = line;
        var startColumn = column;
        var builder = new StringBuilder();
        while (!IsAtEnd && IsIdentifierPart(Peek()))
        {
            builder.Append(Advance());
        }

        var text = builder.ToString();
        var kind = Keywords.Contains(text) ? CppTokenKind.Keyword : CppTokenKind.Identifier;
        return new CppToken(kind, text, startLine, startColumn);
    }

    private CppToken ReadNumberLiteral()
    {
        var startLine = line;
        var startColumn = column;
        var builder = new StringBuilder();

        while (!IsAtEnd)
        {
            var ch = Peek();
            if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '\'' || ch == 'x' || ch == 'X')
            {
                builder.Append(Advance());
                continue;
            }

            break;
        }

        return new CppToken(CppTokenKind.NumberLiteral, builder.ToString(), startLine, startColumn);
    }

    private CppToken ReadQuotedLiteral(char quote, CppTokenKind kind)
    {
        var startLine = line;
        var startColumn = column;
        var builder = new StringBuilder();
        builder.Append(Advance());

        var escaped = false;
        while (!IsAtEnd)
        {
            var ch = Advance();
            builder.Append(ch);

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == quote || ch == '\n')
            {
                break;
            }
        }

        return new CppToken(kind, builder.ToString(), startLine, startColumn);
    }

    private CppToken ReadSymbol()
    {
        var startLine = line;
        var startColumn = column;

        foreach (var symbol in MultiCharSymbols)
        {
            if (!Matches(symbol))
            {
                continue;
            }

            for (var i = 0; i < symbol.Length; i++)
            {
                Advance();
            }

            return new CppToken(CppTokenKind.Symbol, symbol, startLine, startColumn);
        }

        var single = Advance().ToString();
        return new CppToken(CppTokenKind.Symbol, single, startLine, startColumn);
    }

    private void SkipHorizontalWhitespace()
    {
        while (!IsAtEnd)
        {
            var ch = Peek();
            if (ch == ' ' || ch == '\t' || ch == '\f' || ch == '\v')
            {
                Advance();
                continue;
            }

            break;
        }
    }

    private bool Matches(string text)
    {
        if (string.IsNullOrEmpty(text) || position + text.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (source[position + i] != text[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    private static bool IsIdentifierPart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }
}

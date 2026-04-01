namespace C__Editor;

internal enum CppTokenKind
{
    Identifier,
    Keyword,
    NumberLiteral,
    StringLiteral,
    CharLiteral,
    Symbol,
    Preprocessor,
    EndOfFile
}

internal readonly record struct CppToken(
    CppTokenKind Kind,
    string Text,
    int Line,
    int Column,
    string Value = "")
{
    public bool IsSymbol(string symbol)
    {
        return Kind == CppTokenKind.Symbol && string.Equals(Text, symbol, StringComparison.Ordinal);
    }

    public bool IsKeyword(string keyword)
    {
        return Kind == CppTokenKind.Keyword && string.Equals(Text, keyword, StringComparison.Ordinal);
    }

    public bool IsIdentifier(string identifier)
    {
        return Kind == CppTokenKind.Identifier && string.Equals(Text, identifier, StringComparison.Ordinal);
    }
}

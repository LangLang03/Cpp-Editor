using System.Text;
using SweetLine;
using EditorTextRange = SweetEditor.TextRange;
using SweetLineTextPosition = SweetLine.TextPosition;
using SweetLineTextRange = SweetLine.TextRange;

namespace C__Editor;

internal sealed class EditorSyntaxHighlightProvider : SweetEditor.IDecorationProvider
{
    private readonly object syncRoot = new();
    private readonly HighlightEngine highlightEngine;

    private DocumentAnalyzer? documentAnalyzer;
    private DocumentHighlight? cacheHighlight;
    private string sourceFileName = "untitled.cpp";
    private string sourceText = string.Empty;

    internal EditorSyntaxHighlightProvider(IReadOnlyList<string> syntaxFiles)
    {
        if (syntaxFiles is null || syntaxFiles.Count == 0)
        {
            throw new ArgumentException("No syntax files configured.", nameof(syntaxFiles));
        }

        highlightEngine = new HighlightEngine(new HighlightConfig(false, false));
        RegisterStyleMap(highlightEngine);

        foreach (var syntaxFile in syntaxFiles)
        {
            var syntaxJson = File.ReadAllText(syntaxFile);
            highlightEngine.CompileSyntaxFromJson(syntaxJson);
        }
    }

    public SweetEditor.DecorationType Capabilities => SweetEditor.DecorationType.SyntaxHighlight;

    internal void SetDocumentSource(string fileName, string text)
    {
        lock (syncRoot)
        {
            sourceFileName = string.IsNullOrWhiteSpace(fileName) ? "untitled.cpp" : fileName;
            sourceText = text ?? string.Empty;
            documentAnalyzer = null;
            cacheHighlight = null;
        }
    }

    public void ProvideDecorations(SweetEditor.DecorationContext context, SweetEditor.IDecorationReceiver receiver)
    {
        SweetEditor.DecorationResult result;

        lock (syncRoot)
        {
            if (receiver.IsCancelled)
            {
                return;
            }

            EnsureAnalyzer();

            if (documentAnalyzer is null)
            {
                result = CreateEmptySyntaxResult();
            }
            else
            {
                if (context.TextChanges.Count > 0)
                {
                    foreach (var change in context.TextChanges)
                    {
                        if (change.Range == null)
                        {
                            continue;
                        }

                        var newText = change.NewText ?? string.Empty;
                        cacheHighlight = documentAnalyzer.AnalyzeIncremental(ConvertToSweetLineTextRange(change.Range.Value), newText);
                        sourceText = ApplyTextChange(sourceText, change.Range.Value, newText);
                    }
                }
                else if (cacheHighlight is null)
                {
                    cacheHighlight = documentAnalyzer.Analyze();
                }

                result = BuildSyntaxResult(cacheHighlight, context);
            }
        }

        if (!receiver.IsCancelled)
        {
            receiver.Accept(result);
        }
    }

    private void EnsureAnalyzer()
    {
        if (documentAnalyzer is not null)
        {
            return;
        }

        using var document = new SweetLine.Document(BuildAnalysisUri(sourceFileName), sourceText);
        documentAnalyzer = highlightEngine.LoadDocument(document);
        cacheHighlight = documentAnalyzer?.Analyze();
    }

    private static SweetEditor.DecorationResult BuildSyntaxResult(DocumentHighlight? highlight, SweetEditor.DecorationContext context)
    {
        if (highlight?.Lines is null || highlight.Lines.Count == 0)
        {
            return CreateEmptySyntaxResult();
        }

        var syntaxSpans = new Dictionary<int, List<SweetEditor.DecorationResult.SpanItem>>();
        var startLine = Math.Max(0, context.VisibleStartLine);
        var endLine = context.VisibleEndLine >= startLine
            ? Math.Min(context.VisibleEndLine, highlight.Lines.Count - 1)
            : highlight.Lines.Count - 1;

        for (var line = startLine; line <= endLine; line++)
        {
            var lineHighlight = highlight.Lines[line];
            if (lineHighlight?.Spans is null)
            {
                continue;
            }

            foreach (var token in lineHighlight.Spans)
            {
                if (token.StyleId <= 0)
                {
                    continue;
                }

                var range = ExtractSingleLineRange(token);
                if (range is null)
                {
                    continue;
                }

                if (!syntaxSpans.TryGetValue(range.Line, out var spans))
                {
                    spans = new List<SweetEditor.DecorationResult.SpanItem>();
                    syntaxSpans[range.Line] = spans;
                }

                spans.Add(new SweetEditor.DecorationResult.SpanItem(range.StartColumn, range.Length, token.StyleId));
            }
        }

        return new SweetEditor.DecorationResult
        {
            SyntaxSpans = syntaxSpans,
            SyntaxSpansMode = SweetEditor.DecorationApplyMode.REPLACE_RANGE
        };
    }

    private static SweetEditor.DecorationResult CreateEmptySyntaxResult()
    {
        return new SweetEditor.DecorationResult
        {
            SyntaxSpans = new Dictionary<int, List<SweetEditor.DecorationResult.SpanItem>>(),
            SyntaxSpansMode = SweetEditor.DecorationApplyMode.REPLACE_RANGE
        };
    }

    private static void RegisterStyleMap(HighlightEngine engine)
    {
        engine.RegisterStyleName("keyword", (int)SweetEditor.EditorTheme.STYLE_KEYWORD);
        engine.RegisterStyleName("type", (int)SweetEditor.EditorTheme.STYLE_TYPE);
        engine.RegisterStyleName("string", (int)SweetEditor.EditorTheme.STYLE_STRING);
        engine.RegisterStyleName("comment", (int)SweetEditor.EditorTheme.STYLE_COMMENT);
        engine.RegisterStyleName("preprocessor", (int)SweetEditor.EditorTheme.STYLE_PREPROCESSOR);
        engine.RegisterStyleName("macro", (int)SweetEditor.EditorTheme.STYLE_PREPROCESSOR);
        engine.RegisterStyleName("method", (int)SweetEditor.EditorTheme.STYLE_FUNCTION);
        engine.RegisterStyleName("function", (int)SweetEditor.EditorTheme.STYLE_FUNCTION);
        engine.RegisterStyleName("variable", (int)SweetEditor.EditorTheme.STYLE_VARIABLE);
        engine.RegisterStyleName("field", (int)SweetEditor.EditorTheme.STYLE_VARIABLE);
        engine.RegisterStyleName("number", (int)SweetEditor.EditorTheme.STYLE_NUMBER);
        engine.RegisterStyleName("class", (int)SweetEditor.EditorTheme.STYLE_CLASS);
        engine.RegisterStyleName("builtin", (int)SweetEditor.EditorTheme.STYLE_BUILTIN);
        engine.RegisterStyleName("annotation", (int)SweetEditor.EditorTheme.STYLE_ANNOTATION);
    }

    private static string BuildAnalysisUri(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            return new Uri(fileName).AbsoluteUri;
        }

        return $"file:///{fileName}";
    }

    private static SweetLineTextRange ConvertToSweetLineTextRange(EditorTextRange range)
    {
        return new SweetLineTextRange(
            new SweetLineTextPosition(range.Start.Line, range.Start.Column, 0),
            new SweetLineTextPosition(range.End.Line, range.End.Column, 0));
    }

    private static string ApplyTextChange(string originalText, EditorTextRange range, string newText)
    {
        var startOffset = LineColumnToOffset(originalText, range.Start.Line, range.Start.Column);
        var endOffset = LineColumnToOffset(originalText, range.End.Line, range.End.Column);

        if (startOffset > endOffset)
        {
            (startOffset, endOffset) = (endOffset, startOffset);
        }

        var builder = new StringBuilder(Math.Max(0, originalText.Length - (endOffset - startOffset)) + newText.Length);
        builder.Append(originalText, 0, startOffset);
        builder.Append(newText);
        builder.Append(originalText, endOffset, originalText.Length - endOffset);
        return builder.ToString();
    }

    private static int LineColumnToOffset(string text, int targetLine, int targetColumn)
    {
        var line = 0;
        var index = 0;

        while (index < text.Length && line < Math.Max(0, targetLine))
        {
            var ch = text[index++];
            if (ch == '\n')
            {
                line++;
            }
        }

        var column = 0;
        while (index < text.Length && column < Math.Max(0, targetColumn))
        {
            if (text[index] == '\n')
            {
                break;
            }

            index++;
            column++;
        }

        return index;
    }

    private static TokenRangeInfo? ExtractSingleLineRange(TokenSpan token)
    {
        var startLine = token.Range.Start.Line;
        var endLine = token.Range.End.Line;
        var startColumn = token.Range.Start.Column;
        var endColumn = token.Range.End.Column;

        if (startLine < 0 || startLine != endLine || startColumn < 0 || endColumn <= startColumn)
        {
            return null;
        }

        return new TokenRangeInfo(startLine, startColumn, endColumn);
    }

    private sealed class TokenRangeInfo
    {
        internal TokenRangeInfo(int line, int startColumn, int endColumn)
        {
            Line = line;
            StartColumn = startColumn;
            EndColumn = endColumn;
        }

        internal int Line { get; }

        internal int StartColumn { get; }

        internal int EndColumn { get; }

        internal int Length => EndColumn - StartColumn;
    }
}

namespace C__Editor;

public partial class MainEditorForm
{
    private EditorSyntaxHighlightProvider? syntaxHighlightProvider;

    private void InitializeSyntaxHighlighting()
    {
        if (editorControlMain is null)
        {
            return;
        }

        var syntaxFiles = ResolveSyntaxFiles();
        if (syntaxFiles.Count == 0)
        {
            return;
        }

        try
        {
            syntaxHighlightProvider = new EditorSyntaxHighlightProvider(syntaxFiles);
            editorControlMain.AddDecorationProvider(syntaxHighlightProvider);
        }
        catch
        {
            syntaxHighlightProvider = null;
        }
    }

    private void SetEditorSyntaxSource(string fileName, string normalizedText)
    {
        if (syntaxHighlightProvider is null)
        {
            return;
        }

        syntaxHighlightProvider.SetDocumentSource(fileName, normalizedText);
    }

    private static List<string> ResolveSyntaxFiles()
    {
        var candidates = new List<string>();

        try
        {
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "editor", "syntaxes"));
        }
        catch
        {
            // Ignore invalid base directory.
        }

        try
        {
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "editor", "syntaxes"));
        }
        catch
        {
            // Ignore invalid current directory.
        }

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 6 && dir is not null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, "editor", "syntaxes"));
                dir = dir.Parent;
            }
        }
        catch
        {
            // Ignore traversal issues.
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var files = Directory
                .EnumerateFiles(candidate, "*.json", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count > 0)
            {
                return files;
            }
        }

        return new List<string>();
    }
}

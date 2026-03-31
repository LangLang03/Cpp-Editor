using System.Text;
using System.Text.Json;

namespace C__Editor;

internal static class EditorSessionStateController
{
    private static readonly object SyncRoot = new();

    internal static EditorSessionState Load()
    {
        lock (SyncRoot)
        {
            try
            {
                var path = GetSessionStatePath();
                if (!File.Exists(path))
                {
                    return new EditorSessionState();
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<EditorSessionState>(json);
                if (loaded is null)
                {
                    return new EditorSessionState();
                }

                return Normalize(loaded);
            }
            catch
            {
                return new EditorSessionState();
            }
        }
    }

    internal static void Save(EditorSessionState state)
    {
        lock (SyncRoot)
        {
            try
            {
                var normalized = Normalize(state ?? new EditorSessionState());
                var path = GetSessionStatePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch
            {
                // Keep runtime behavior even if persistence fails.
            }
        }
    }

    internal static void Clear()
    {
        lock (SyncRoot)
        {
            try
            {
                var path = GetSessionStatePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static EditorSessionState Normalize(EditorSessionState state)
    {
        var normalized = new EditorSessionState
        {
            Version = state.Version <= 0 ? 1 : state.Version,
            OpenedFolderPaths = NormalizePathList(state.OpenedFolderPaths),
            OpenedFilePaths = NormalizePathList(state.OpenedFilePaths),
            OpenDocumentPaths = NormalizePathList(state.OpenDocumentPaths),
            ActiveDocumentPath = NormalizeSinglePath(state.ActiveDocumentPath),
            SelectedExplorerPath = NormalizeSinglePath(state.SelectedExplorerPath),
            ActiveCursorLine = Math.Max(0, state.ActiveCursorLine),
            ActiveCursorColumn = Math.Max(0, state.ActiveCursorColumn)
        };

        return normalized;
    }

    private static List<string> NormalizePathList(List<string>? paths)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (paths is null)
        {
            return result;
        }

        foreach (var path in paths)
        {
            var normalized = NormalizeSinglePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static string NormalizeSinglePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetSessionStatePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "C++Editor", "settings", "session.json");
    }
}

internal sealed class EditorSessionState
{
    public int Version { get; set; } = 1;

    public List<string> OpenedFolderPaths { get; set; } = new();

    public List<string> OpenedFilePaths { get; set; } = new();

    public List<string> OpenDocumentPaths { get; set; } = new();

    public string ActiveDocumentPath { get; set; } = string.Empty;

    public string SelectedExplorerPath { get; set; } = string.Empty;

    public int ActiveCursorLine { get; set; }

    public int ActiveCursorColumn { get; set; }
}

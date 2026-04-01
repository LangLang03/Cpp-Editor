using System.Text;
using System.Text.Json;

namespace C__Editor;

internal static class WorkspaceBreakpointMarkerController
{
    private const int CurrentVersion = 1;
    private const string ConfigDirectoryName = ".cppeditor";
    private const string ConfigFileName = "breakpoints.json";

    internal static IReadOnlyList<int> LoadLines(string workspaceRoot, string filePath)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return Array.Empty<int>();
        }

        var normalizedFilePath = NormalizeAbsolutePath(filePath);
        if (string.IsNullOrWhiteSpace(normalizedFilePath))
        {
            return Array.Empty<int>();
        }

        var config = LoadConfig(normalizedRoot);
        var key = NormalizeFileKey(normalizedRoot, normalizedFilePath);
        if (string.IsNullOrWhiteSpace(key) || !config.LinesByFile.TryGetValue(key, out var lines))
        {
            return Array.Empty<int>();
        }

        return NormalizeLines(lines);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<int>> LoadAllLines(string workspaceRoot)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase);
        }

        var config = LoadConfig(normalizedRoot);
        var result = new Dictionary<string, IReadOnlyList<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in config.LinesByFile)
        {
            var normalizedKey = NormalizeStoredFileKey(normalizedRoot, pair.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            var absolutePath = Path.IsPathRooted(normalizedKey)
                ? NormalizeAbsolutePath(normalizedKey)
                : NormalizeAbsolutePath(Path.Combine(normalizedRoot, normalizedKey));
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                continue;
            }

            var lines = NormalizeLines(pair.Value);
            if (lines.Count == 0)
            {
                continue;
            }

            result[absolutePath] = lines;
        }

        return result;
    }

    internal static void SaveLines(string workspaceRoot, string filePath, IReadOnlyCollection<int> lines)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return;
        }

        var normalizedFilePath = NormalizeAbsolutePath(filePath);
        var key = NormalizeFileKey(normalizedRoot, normalizedFilePath);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var config = LoadConfig(normalizedRoot);
        var normalizedLines = NormalizeLines(lines);
        if (normalizedLines.Count == 0)
        {
            config.LinesByFile.Remove(key);
        }
        else
        {
            config.LinesByFile[key] = normalizedLines;
        }

        SaveConfig(normalizedRoot, config);
    }

    internal static string GetConfigPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, ConfigDirectoryName, ConfigFileName);
    }

    private static WorkspaceBreakpointMarkerConfig LoadConfig(string normalizedRoot)
    {
        try
        {
            var path = GetConfigPath(normalizedRoot);
            if (!File.Exists(path))
            {
                return new WorkspaceBreakpointMarkerConfig();
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var loaded = JsonSerializer.Deserialize<WorkspaceBreakpointMarkerConfig>(json);
            var normalized = NormalizeConfig(normalizedRoot, loaded);
            if (NeedsAbsolutePathMigration(loaded))
            {
                SaveConfig(normalizedRoot, normalized);
            }

            return normalized;
        }
        catch
        {
            return new WorkspaceBreakpointMarkerConfig();
        }
    }

    private static void SaveConfig(string normalizedRoot, WorkspaceBreakpointMarkerConfig config)
    {
        try
        {
            var path = GetConfigPath(normalizedRoot);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var normalized = NormalizeConfig(normalizedRoot, config);
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

    private static WorkspaceBreakpointMarkerConfig NormalizeConfig(string workspaceRoot, WorkspaceBreakpointMarkerConfig? config)
    {
        var result = new WorkspaceBreakpointMarkerConfig
        {
            Version = CurrentVersion
        };

        if (config?.LinesByFile is null)
        {
            return result;
        }

        foreach (var pair in config.LinesByFile)
        {
            var key = NormalizeStoredFileKey(workspaceRoot, pair.Key);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var lines = NormalizeLines(pair.Value);
            if (lines.Count == 0)
            {
                continue;
            }

            result.LinesByFile[key] = lines;
        }

        return result;
    }

    private static List<int> NormalizeLines(IEnumerable<int>? lines)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        if (lines is null)
        {
            return result;
        }

        foreach (var line in lines)
        {
            if (line <= 0 || !seen.Add(line))
            {
                continue;
            }

            result.Add(line);
        }

        result.Sort();
        return result;
    }

    private static string NormalizeStoredFileKey(string workspaceRoot, string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return string.Empty;
        }

        try
        {
            var normalizedSeparators = rawKey
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var absolute = Path.IsPathRooted(normalizedSeparators)
                ? Path.GetFullPath(normalizedSeparators)
                : Path.GetFullPath(Path.Combine(workspaceRoot, normalizedSeparators));

            return NormalizeFileKey(workspaceRoot, absolute);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeFileKey(string workspaceRoot, string filePath)
    {
        var absolutePath = NormalizeAbsolutePath(filePath);
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        // Persist as absolute path to avoid ambiguity when opening the same
        // file name from different directories.
        return absolutePath;
    }

    private static string NormalizeAbsolutePath(string? path)
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

    private static bool NeedsAbsolutePathMigration(WorkspaceBreakpointMarkerConfig? config)
    {
        if (config?.LinesByFile is null || config.LinesByFile.Count == 0)
        {
            return false;
        }

        foreach (var key in config.LinesByFile.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!Path.IsPathRooted(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalizeWorkspaceRoot(string workspaceRoot, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return false;
        }

        try
        {
            normalizedRoot = Path.GetFullPath(workspaceRoot.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class WorkspaceBreakpointMarkerConfig
{
    public int Version { get; set; } = 1;

    public Dictionary<string, List<int>> LinesByFile { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

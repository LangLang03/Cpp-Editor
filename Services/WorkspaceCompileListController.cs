using System.Text;
using System.Text.Json;

namespace C__Editor;

internal static class WorkspaceCompileListController
{
    private const int CurrentVersion = 1;
    private const string ConfigDirectoryName = ".cppeditor";
    private const string ConfigFileName = "compile-list.json";

    internal static WorkspaceCompileListConfig Load(string workspaceRoot)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return WorkspaceCompileListConfig.CreateDefault();
        }

        try
        {
            var path = GetConfigPath(normalizedRoot);
            if (!File.Exists(path))
            {
                return WorkspaceCompileListConfig.CreateDefault();
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var loaded = JsonSerializer.Deserialize<WorkspaceCompileListConfig>(json);
            return Normalize(loaded);
        }
        catch
        {
            return WorkspaceCompileListConfig.CreateDefault();
        }
    }

    internal static void Save(string workspaceRoot, IReadOnlyList<string> patterns)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return;
        }

        try
        {
            var config = new WorkspaceCompileListConfig
            {
                Version = CurrentVersion,
                Include = NormalizePatterns(patterns)
            };

            var path = GetConfigPath(normalizedRoot);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
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

    internal static IReadOnlyList<string> ResolveFiles(string workspaceRoot, IReadOnlyList<string> patterns)
    {
        if (!TryNormalizeWorkspaceRoot(workspaceRoot, out var normalizedRoot))
        {
            return Array.Empty<string>();
        }

        var normalizedPatterns = NormalizePatterns(patterns);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in normalizedPatterns)
        {
            foreach (var filePath in ExpandPattern(normalizedRoot, pattern))
            {
                if (seen.Add(filePath))
                {
                    result.Add(filePath);
                }
            }
        }

        return result;
    }

    internal static IReadOnlyList<string> ParsePatternsFromText(string text)
    {
        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToList();

        return NormalizePatterns(lines);
    }

    internal static string ToMultilineText(IReadOnlyList<string> patterns)
    {
        return string.Join(Environment.NewLine, NormalizePatterns(patterns));
    }

    internal static string GetConfigPath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, ConfigDirectoryName, ConfigFileName);
    }

    private static WorkspaceCompileListConfig Normalize(WorkspaceCompileListConfig? config)
    {
        var input = config ?? WorkspaceCompileListConfig.CreateDefault();
        return new WorkspaceCompileListConfig
        {
            Version = input.Version <= 0 ? CurrentVersion : input.Version,
            Include = NormalizePatterns(input.Include)
        };
    }

    private static List<string> NormalizePatterns(IReadOnlyList<string>? patterns)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (patterns is null)
        {
            return result;
        }

        foreach (var rawPattern in patterns)
        {
            var trimmed = (rawPattern ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static IEnumerable<string> ExpandPattern(string workspaceRoot, string pattern)
    {
        var normalizedPattern = pattern.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (normalizedPattern.IndexOfAny(new[] { '*', '?' }) >= 0)
        {
            var relativeDirectory = Path.GetDirectoryName(normalizedPattern) ?? string.Empty;
            var filePattern = Path.GetFileName(normalizedPattern);
            if (string.IsNullOrWhiteSpace(filePattern))
            {
                yield break;
            }

            string searchDirectory;
            try
            {
                searchDirectory = Path.IsPathRooted(relativeDirectory)
                    ? Path.GetFullPath(relativeDirectory)
                    : Path.GetFullPath(Path.Combine(workspaceRoot, relativeDirectory));
            }
            catch
            {
                yield break;
            }

            if (!Directory.Exists(searchDirectory))
            {
                yield break;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(searchDirectory, filePattern, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                yield break;
            }

            foreach (var file in files)
            {
                yield return Path.GetFullPath(file);
            }

            yield break;
        }

        string candidatePath;
        try
        {
            candidatePath = Path.IsPathRooted(normalizedPattern)
                ? Path.GetFullPath(normalizedPattern)
                : Path.GetFullPath(Path.Combine(workspaceRoot, normalizedPattern));
        }
        catch
        {
            yield break;
        }

        if (File.Exists(candidatePath))
        {
            yield return candidatePath;
        }
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

internal sealed class WorkspaceCompileListConfig
{
    public int Version { get; set; } = 1;

    public List<string> Include { get; set; } = new();

    internal static WorkspaceCompileListConfig CreateDefault()
    {
        return new WorkspaceCompileListConfig
        {
            Version = 1,
            Include = new List<string>()
        };
    }
}

namespace C__Editor;

internal static class EditorAutoPairController
{
    private const string DefaultPairFormat = "<>{}()";
    private static readonly object SyncRoot = new();

    private static IReadOnlyList<SweetEditor.BracketPair>? cachedPairs;
    private static string? cachedPairFormat;

    internal static SweetEditor.LanguageConfiguration BuildLanguageConfiguration(string? filePath)
    {
        var languageId = ResolveLanguageId(filePath);
        var autoClosingPairs = GetAutoClosingPairs();
        // Native bracket matching in sweeteditor is unstable for symmetric pairs like "" or ''.
        // Keep them for managed auto-closing, but do not send them to native bracket pairs.
        var bracketPairs = autoClosingPairs
            .Where(pair => !string.Equals(pair.Open, pair.Close, StringComparison.Ordinal))
            .ToList();

        return new SweetEditor.LanguageConfiguration(languageId, bracketPairs, autoClosingPairs);
    }

    internal static string GetPairFormat()
    {
        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(cachedPairFormat))
            {
                return cachedPairFormat;
            }

            var config = EditorConfigurationController.GetAutoPairSettings();
            cachedPairFormat = NormalizePairFormat(config.PairFormat);
            return cachedPairFormat;
        }
    }

    internal static void SetPairFormat(string? pairFormat)
    {
        lock (SyncRoot)
        {
            var normalized = NormalizePairFormat(pairFormat);
            var pairs = ParsePairFormat(normalized);
            if (pairs.Count == 0)
            {
                normalized = DefaultPairFormat;
                pairs = ParsePairFormat(normalized);
            }

            var config = new AutoPairSettingsConfig
            {
                PairFormat = normalized,
                ParsedFromFormat = normalized,
                ParsedPairs = pairs
                    .Select(pair => new AutoPairItemConfig
                    {
                        Open = pair.Open,
                        Close = pair.Close
                    })
                    .ToList()
            };

            EditorConfigurationController.SaveAutoPairSettings(config);
            cachedPairFormat = normalized;
            cachedPairs = pairs;
        }
    }

    private static IReadOnlyList<SweetEditor.BracketPair> GetAutoClosingPairs()
    {
        lock (SyncRoot)
        {
            if (cachedPairs is not null)
            {
                return cachedPairs;
            }

            var config = EditorConfigurationController.GetAutoPairSettings();
            config.PairFormat = NormalizePairFormat(config.PairFormat);
            cachedPairFormat = config.PairFormat;

            List<SweetEditor.BracketPair> pairs;
            if (HasUsableCachedPairs(config))
            {
                pairs = config.ParsedPairs
                    .Where(item => item.Open?.Length == 1 && item.Close?.Length == 1)
                    .Select(item => new SweetEditor.BracketPair(item.Open, item.Close))
                    .ToList();
            }
            else
            {
                pairs = ParsePairFormat(config.PairFormat);
                config.ParsedPairs = pairs
                    .Select(pair => new AutoPairItemConfig { Open = pair.Open, Close = pair.Close })
                    .ToList();
                config.ParsedFromFormat = config.PairFormat;
                EditorConfigurationController.SaveAutoPairSettings(config);
            }

            if (pairs.Count == 0)
            {
                pairs = ParsePairFormat(DefaultPairFormat);
            }

            cachedPairs = pairs;
            return cachedPairs;
        }
    }

    private static bool HasUsableCachedPairs(AutoPairSettingsConfig config)
    {
        if (config.ParsedPairs is null || config.ParsedPairs.Count == 0)
        {
            return false;
        }

        if (!string.Equals(config.ParsedFromFormat, config.PairFormat, StringComparison.Ordinal))
        {
            return false;
        }

        return config.ParsedPairs.All(item => item.Open?.Length == 1 && item.Close?.Length == 1);
    }

    private static List<SweetEditor.BracketPair> ParsePairFormat(string pairFormat)
    {
        var normalized = NormalizePairFormat(pairFormat);
        if (normalized.Length < 2 || normalized.Length % 2 != 0)
        {
            normalized = DefaultPairFormat;
        }

        var pairs = new List<SweetEditor.BracketPair>();
        var seenOpen = new HashSet<char>();
        for (var i = 0; i + 1 < normalized.Length; i += 2)
        {
            var open = normalized[i];
            var close = normalized[i + 1];
            if (!seenOpen.Add(open))
            {
                continue;
            }

            pairs.Add(new SweetEditor.BracketPair(open.ToString(), close.ToString()));
        }

        return pairs;
    }

    private static string NormalizePairFormat(string? pairFormat)
    {
        if (string.IsNullOrWhiteSpace(pairFormat))
        {
            return DefaultPairFormat;
        }

        var compact = new string(pairFormat.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return string.IsNullOrWhiteSpace(compact) ? DefaultPairFormat : compact;
    }

    private static string ResolveLanguageId(string? filePath)
    {
        var extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".c" or ".h" => "c",
            ".cpp" or ".cxx" or ".cc" or ".hpp" or ".hxx" or ".hh" or ".h++" or ".ipp" => "cpp",
            ".json" or ".jsonc" or ".json5" => "json",
            ".xml" or ".xsd" or ".xsl" or ".xslt" or ".svg" or ".plist" or ".props" or ".targets" or ".config" or ".csproj" or ".vbproj" => "xml",
            _ => "cpp"
        };
    }
}

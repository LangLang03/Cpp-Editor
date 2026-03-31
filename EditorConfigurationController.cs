using System.Text.Json;

namespace C__Editor;

internal static class EditorConfigurationController
{
    private const int CurrentVersion = 3;
    private const string DefaultAutoPairFormat = "<>{}()";
    private static readonly object SyncRoot = new();

    private static EditorAppConfig? cachedConfig;

    internal static UiSettings GetUiSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().Ui.Clone();
        }
    }

    internal static void SaveUiSettings(UiSettings settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.Ui = settings?.Clone() ?? new UiSettings();
            SaveNormalized(config);
        }
    }

    internal static AutoPairSettingsConfig GetAutoPairSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().AutoPairs.Clone();
        }
    }

    internal static void SaveAutoPairSettings(AutoPairSettingsConfig settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.AutoPairs = settings?.Clone() ?? new AutoPairSettingsConfig();
            SaveNormalized(config);
        }
    }

    internal static IReadOnlyDictionary<string, string> GetShortcutGestures()
    {
        lock (SyncRoot)
        {
            return new Dictionary<string, string>(GetConfigClone().Shortcuts.GestureByCommandId, StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static void SaveShortcutGestures(IReadOnlyDictionary<string, string> gestures)
    {
        lock (SyncRoot)
        {
            var nextMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (gestures is not null)
            {
                foreach (var pair in gestures)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    if (!EditorShortcutKeyFormatter.TryParse(pair.Value, out var keys))
                    {
                        continue;
                    }

                    nextMap[pair.Key] = EditorShortcutKeyFormatter.ToDisplayString(keys);
                }
            }

            var config = GetConfigClone();
            config.Shortcuts = new ShortcutSettingsSection
            {
                GestureByCommandId = nextMap
            };
            SaveNormalized(config);
        }
    }

    private static EditorAppConfig GetConfigClone()
    {
        if (cachedConfig is null)
        {
            var (loaded, shouldSave) = LoadOrCreateConfig();
            cachedConfig = loaded;
            if (shouldSave)
            {
                SaveToDisk(loaded);
            }
        }

        return cachedConfig.Clone();
    }

    private static (EditorAppConfig config, bool shouldSave) LoadOrCreateConfig()
    {
        if (TryReadConfig(GetUnifiedConfigPath(), out var loadedConfig) && loadedConfig is not null)
        {
            return Normalize(loadedConfig);
        }

        var migrated = BuildConfigFromLegacyFiles();
        var normalized = Normalize(migrated);
        return (normalized.config, true);
    }

    private static EditorAppConfig BuildConfigFromLegacyFiles()
    {
        var uiSettings = TryReadConfig<UiSettings>(GetLegacyUiConfigPath()) ?? new UiSettings();
        var autoPairs = TryReadConfig<AutoPairSettingsConfig>(GetLegacyAutoPairConfigPath()) ?? new AutoPairSettingsConfig();

        var shortcuts = new ShortcutSettingsSection
        {
            GestureByCommandId = new Dictionary<string, string>(EditorShortcutCatalog.CreateDefaultGestureMap(), StringComparer.OrdinalIgnoreCase)
        };

        return new EditorAppConfig
        {
            ConfigVersion = CurrentVersion,
            Ui = uiSettings,
            AutoPairs = autoPairs,
            Shortcuts = shortcuts
        };
    }

    private static void SaveNormalized(EditorAppConfig config)
    {
        var normalized = Normalize(config).config;
        cachedConfig = normalized.Clone();
        SaveToDisk(normalized);
    }

    private static (EditorAppConfig config, bool shouldSave) Normalize(EditorAppConfig config)
    {
        var shouldSave = false;
        var normalized = config.Clone();
        var originalVersion = normalized.ConfigVersion;

        if (normalized.ConfigVersion != CurrentVersion)
        {
            normalized.ConfigVersion = CurrentVersion;
            shouldSave = true;
        }

        var ui = normalized.Ui ?? new UiSettings();
        var normalizedUi = new UiSettings
        {
            ShowProjectTree = ui.ShowProjectTree,
            ShowOutputPanel = ui.ShowOutputPanel,
            ExplorerWidth = Math.Clamp(ui.ExplorerWidth, 180, 420)
        };
        if (!UiEquals(ui, normalizedUi))
        {
            shouldSave = true;
        }

        normalized.Ui = normalizedUi;

        var autoPairs = normalized.AutoPairs ?? new AutoPairSettingsConfig();
        var normalizedPairFormat = NormalizePairFormat(autoPairs.PairFormat);
        if (!string.Equals(normalizedPairFormat, autoPairs.PairFormat, StringComparison.Ordinal))
        {
            shouldSave = true;
        }

        normalized.AutoPairs = new AutoPairSettingsConfig
        {
            PairFormat = normalizedPairFormat,
            ParsedFromFormat = autoPairs.ParsedFromFormat ?? string.Empty,
            ParsedPairs = NormalizeParsedPairs(autoPairs.ParsedPairs)
        };

        var normalizedShortcuts = NormalizeShortcutSection(normalized.Shortcuts);
        if (originalVersion < 3)
        {
            var migrated = MigrateShortcutsToV3(normalizedShortcuts);
            if (!ShortcutSectionEquals(normalizedShortcuts, migrated))
            {
                normalizedShortcuts = migrated;
                shouldSave = true;
            }
        }

        if (!ShortcutSectionEquals(normalized.Shortcuts, normalizedShortcuts))
        {
            shouldSave = true;
        }

        normalized.Shortcuts = normalizedShortcuts;

        return (normalized, shouldSave);
    }

    private static List<AutoPairItemConfig> NormalizeParsedPairs(List<AutoPairItemConfig>? parsedPairs)
    {
        if (parsedPairs is null || parsedPairs.Count == 0)
        {
            return new List<AutoPairItemConfig>();
        }

        var normalized = new List<AutoPairItemConfig>();
        foreach (var item in parsedPairs)
        {
            if (item?.Open?.Length != 1 || item.Close?.Length != 1)
            {
                continue;
            }

            normalized.Add(new AutoPairItemConfig
            {
                Open = item.Open,
                Close = item.Close
            });
        }

        return normalized;
    }

    private static ShortcutSettingsSection NormalizeShortcutSection(ShortcutSettingsSection? section)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in EditorShortcutCatalog.CreateDefaultGestureMap())
        {
            if (EditorShortcutKeyFormatter.TryParse(pair.Value, out var keys))
            {
                result[pair.Key] = EditorShortcutKeyFormatter.ToDisplayString(keys);
            }
            else
            {
                result[pair.Key] = string.Empty;
            }
        }

        if (section?.GestureByCommandId is not null)
        {
            foreach (var pair in section.GestureByCommandId)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (!EditorShortcutKeyFormatter.TryParse(pair.Value, out var keys))
                {
                    continue;
                }

                result[pair.Key] = EditorShortcutKeyFormatter.ToDisplayString(keys);
            }
        }

        return new ShortcutSettingsSection
        {
            GestureByCommandId = result
        };
    }

    private static ShortcutSettingsSection MigrateShortcutsToV3(ShortcutSettingsSection section)
    {
        var map = new Dictionary<string, string>(section.GestureByCommandId, StringComparer.OrdinalIgnoreCase);

        UpdateLegacyExplorerGesture(map, EditorCommandIds.ExplorerNewFile, "Ctrl+N");
        UpdateLegacyExplorerGesture(map, EditorCommandIds.ExplorerNewFolder, "Ctrl+Shift+N");
        UpdateLegacyExplorerGesture(map, EditorCommandIds.ExplorerCopy, "Ctrl+C");
        UpdateLegacyExplorerGesture(map, EditorCommandIds.ExplorerPaste, "Ctrl+V");
        UpdateLegacyExplorerGesture(map, EditorCommandIds.ExplorerRefresh, "F5");

        return new ShortcutSettingsSection
        {
            GestureByCommandId = map
        };
    }

    private static void UpdateLegacyExplorerGesture(Dictionary<string, string> map, string commandId, string oldGesture)
    {
        if (!map.TryGetValue(commandId, out var currentGesture))
        {
            return;
        }

        if (!EditorShortcutKeyFormatter.TryParse(currentGesture, out var currentKeys) ||
            !EditorShortcutKeyFormatter.TryParse(oldGesture, out var oldKeys))
        {
            return;
        }

        var normalizedCurrent = EditorShortcutKeyFormatter.ToDisplayString(currentKeys);
        var normalizedOld = EditorShortcutKeyFormatter.ToDisplayString(oldKeys);
        if (string.Equals(normalizedCurrent, normalizedOld, StringComparison.Ordinal))
        {
            map[commandId] = string.Empty;
        }
    }

    private static bool UiEquals(UiSettings left, UiSettings right)
    {
        return left.ShowProjectTree == right.ShowProjectTree &&
               left.ShowOutputPanel == right.ShowOutputPanel &&
               left.ExplorerWidth == right.ExplorerWidth;
    }

    private static bool ShortcutSectionEquals(ShortcutSettingsSection? left, ShortcutSettingsSection right)
    {
        if (left?.GestureByCommandId is null)
        {
            return false;
        }

        if (left.GestureByCommandId.Count != right.GestureByCommandId.Count)
        {
            return false;
        }

        foreach (var pair in right.GestureByCommandId)
        {
            if (!left.GestureByCommandId.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadConfig(string path, out EditorAppConfig? config)
    {
        config = TryReadConfig<EditorAppConfig>(path);
        return config is not null;
    }

    private static T? TryReadConfig<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private static void SaveToDisk(EditorAppConfig config)
    {
        try
        {
            var path = GetUnifiedConfigPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }
        catch
        {
            // Keep runtime behavior even if persistence fails.
        }
    }

    private static string NormalizePairFormat(string? pairFormat)
    {
        if (string.IsNullOrWhiteSpace(pairFormat))
        {
            return DefaultAutoPairFormat;
        }

        var compact = new string(pairFormat.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return string.IsNullOrWhiteSpace(compact) ? DefaultAutoPairFormat : compact;
    }

    private static string GetSettingsRootPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "C++Editor", "settings");
    }

    private static string GetUnifiedConfigPath()
    {
        return Path.Combine(GetSettingsRootPath(), "editor.config.json");
    }

    private static string GetLegacyUiConfigPath()
    {
        return Path.Combine(GetSettingsRootPath(), "ui.json");
    }

    private static string GetLegacyAutoPairConfigPath()
    {
        return Path.Combine(GetSettingsRootPath(), "autopairs.json");
    }
}

internal sealed class EditorAppConfig
{
    public int ConfigVersion { get; set; } = 3;

    public UiSettings Ui { get; set; } = new();

    public AutoPairSettingsConfig AutoPairs { get; set; } = new();

    public ShortcutSettingsSection Shortcuts { get; set; } = new();

    internal EditorAppConfig Clone()
    {
        return new EditorAppConfig
        {
            ConfigVersion = ConfigVersion,
            Ui = Ui?.Clone() ?? new UiSettings(),
            AutoPairs = AutoPairs?.Clone() ?? new AutoPairSettingsConfig(),
            Shortcuts = Shortcuts?.Clone() ?? new ShortcutSettingsSection()
        };
    }
}

internal sealed class AutoPairSettingsConfig
{
    public string PairFormat { get; set; } = "<>{}()";

    public string ParsedFromFormat { get; set; } = string.Empty;

    public List<AutoPairItemConfig> ParsedPairs { get; set; } = new();

    internal AutoPairSettingsConfig Clone()
    {
        return new AutoPairSettingsConfig
        {
            PairFormat = PairFormat,
            ParsedFromFormat = ParsedFromFormat,
            ParsedPairs = ParsedPairs?.Select(item => item.Clone()).ToList() ?? new List<AutoPairItemConfig>()
        };
    }
}

internal sealed class AutoPairItemConfig
{
    public string Open { get; set; } = string.Empty;

    public string Close { get; set; } = string.Empty;

    internal AutoPairItemConfig Clone()
    {
        return new AutoPairItemConfig
        {
            Open = Open,
            Close = Close
        };
    }
}

internal sealed class ShortcutSettingsSection
{
    public Dictionary<string, string> GestureByCommandId { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    internal ShortcutSettingsSection Clone()
    {
        return new ShortcutSettingsSection
        {
            GestureByCommandId = new Dictionary<string, string>(GestureByCommandId, StringComparer.OrdinalIgnoreCase)
        };
    }
}

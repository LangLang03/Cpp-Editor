using System.Text;
using System.Text.Json;

namespace C__Editor;

internal static class EditorConfigurationController
{
    private const int CurrentVersion = 10;
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

    internal static ToolchainSettingsConfig GetToolchainSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().Toolchain.Clone();
        }
    }

    internal static void SaveToolchainSettings(ToolchainSettingsConfig settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.Toolchain = settings?.Clone() ?? ToolchainSettingsConfig.CreateDefault();
            SaveNormalized(config);
        }
    }

    internal static ExplorerSettingsConfig GetExplorerSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().Explorer.Clone();
        }
    }

    internal static void SaveExplorerSettings(ExplorerSettingsConfig settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.Explorer = settings?.Clone() ?? new ExplorerSettingsConfig();
            SaveNormalized(config);
        }
    }

    internal static CppTemplateSettingsConfig GetCppTemplateSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().CppTemplates.Clone();
        }
    }

    internal static void SaveCppTemplateSettings(CppTemplateSettingsConfig settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.CppTemplates = settings?.Clone() ?? CppTemplateSettingsConfig.CreateDefault();
            SaveNormalized(config);
        }
    }

    internal static BuildConfigurationSettings GetBuildConfigurationSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().BuildConfiguration.Clone();
        }
    }

    internal static void SaveBuildConfigurationSettings(BuildConfigurationSettings settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.BuildConfiguration = settings?.Clone() ?? BuildConfigurationSettings.CreateDefault();
            SaveNormalized(config);
        }
    }

    internal static CodeSnippetSettings GetCodeSnippetSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().CodeSnippets.Clone();
        }
    }

    internal static void SaveCodeSnippetSettings(CodeSnippetSettings settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.CodeSnippets = settings?.Clone() ?? CodeSnippetSettings.CreateDefault();
            SaveNormalized(config);
        }
    }

    internal static CodeStructureSettings GetCodeStructureSettings()
    {
        lock (SyncRoot)
        {
            return GetConfigClone().CodeStructure.Clone();
        }
    }

    internal static void SaveCodeStructureSettings(CodeStructureSettings settings)
    {
        lock (SyncRoot)
        {
            var config = GetConfigClone();
            config.CodeStructure = settings?.Clone() ?? CodeStructureSettings.CreateDefault();
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
            Shortcuts = shortcuts,
            Toolchain = ToolchainSettingsConfig.CreateDefault(),
            Explorer = new ExplorerSettingsConfig(),
            CppTemplates = CppTemplateSettingsConfig.CreateDefault()
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
            ExplorerWidth = Math.Clamp(ui.ExplorerWidth, UiSettings.ExplorerWidthMin, UiSettings.ExplorerWidthMax),
            OutputPanelHeight = Math.Clamp(ui.OutputPanelHeight, UiSettings.OutputPanelHeightMin, UiSettings.OutputPanelHeightMax),
            CodeStructurePanelWidth = Math.Clamp(ui.CodeStructurePanelWidth, UiSettings.CodeStructurePanelWidthMin, UiSettings.CodeStructurePanelWidthMax),
            RestoreLastSessionOnStartup = ui.RestoreLastSessionOnStartup,
            ThemeId = EditorThemeController.NormalizeThemeId(ui.ThemeId)
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

        var normalizedToolchain = NormalizeToolchainSection(normalized.Toolchain);
        if (!ToolchainSectionEquals(normalized.Toolchain, normalizedToolchain))
        {
            shouldSave = true;
        }

        normalized.Toolchain = normalizedToolchain;

        var normalizedExplorer = NormalizeExplorerSection(normalized.Explorer);
        if (!ExplorerSectionEquals(normalized.Explorer, normalizedExplorer))
        {
            shouldSave = true;
        }

        normalized.Explorer = normalizedExplorer;

        var normalizedCppTemplates = NormalizeCppTemplateSection(normalized.CppTemplates);
        if (!CppTemplateSectionEquals(normalized.CppTemplates, normalizedCppTemplates))
        {
            shouldSave = true;
        }

        normalized.CppTemplates = normalizedCppTemplates;

        var normalizedBuildConfig = NormalizeBuildConfigurationSection(normalized.BuildConfiguration);
        if (!BuildConfigurationSectionEquals(normalized.BuildConfiguration, normalizedBuildConfig))
        {
            shouldSave = true;
        }

        normalized.BuildConfiguration = normalizedBuildConfig;

        var normalizedCodeSnippets = NormalizeCodeSnippetSection(normalized.CodeSnippets);
        if (!CodeSnippetSectionEquals(normalized.CodeSnippets, normalizedCodeSnippets))
        {
            shouldSave = true;
        }

        normalized.CodeSnippets = normalizedCodeSnippets;

        var normalizedCodeStructure = NormalizeCodeStructureSection(normalized.CodeStructure);
        if (!CodeStructureSectionEquals(normalized.CodeStructure, normalizedCodeStructure))
        {
            shouldSave = true;
        }

        normalized.CodeStructure = normalizedCodeStructure;

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
               left.ExplorerWidth == right.ExplorerWidth &&
               left.OutputPanelHeight == right.OutputPanelHeight &&
               left.CodeStructurePanelWidth == right.CodeStructurePanelWidth &&
               left.RestoreLastSessionOnStartup == right.RestoreLastSessionOnStartup &&
               string.Equals(
                   EditorThemeController.NormalizeThemeId(left.ThemeId),
                   EditorThemeController.NormalizeThemeId(right.ThemeId),
                   StringComparison.Ordinal);
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

    private static ToolchainSettingsConfig NormalizeToolchainSection(ToolchainSettingsConfig? section)
    {
        var input = section ?? ToolchainSettingsConfig.CreateDefault();
        var selectedToolchainId = ResolveSelectedToolchainId(input);
        var selectedKey = ToolchainCatalog.ToConfigValue(selectedToolchainId);
        var debuggerPath = NormalizeDebuggerPath(input.DebuggerPath, input.GdbPath);

        var argumentsByToolchain = ToolchainCatalog.CreateDefaultArgumentsMap();
        if (input.ArgumentsByToolchain is not null)
        {
            foreach (var pair in input.ArgumentsByToolchain)
            {
                if (!ToolchainCatalog.TryParseId(pair.Key, out var parsedId))
                {
                    continue;
                }

                argumentsByToolchain[ToolchainCatalog.ToConfigValue(parsedId)] =
                    NormalizeToolchainArguments(parsedId, pair.Value);
            }
        }

        var selectedArguments = NormalizeToolchainArguments(selectedToolchainId, input.CompilerArguments);
        if (input.ArgumentsByToolchain is null || input.ArgumentsByToolchain.Count == 0)
        {
            argumentsByToolchain[selectedKey] = selectedArguments;
        }
        else if (!argumentsByToolchain.TryGetValue(selectedKey, out var existingValue) ||
                 string.IsNullOrWhiteSpace(existingValue))
        {
            argumentsByToolchain[selectedKey] = selectedArguments;
        }

        return new ToolchainSettingsConfig
        {
            SelectedToolchainId = selectedKey,
            ArgumentsByToolchain = argumentsByToolchain,
            CompilerPath = string.Empty,
            SetupScriptPath = string.Empty,
            ToolchainRootPath = string.Empty,
            CompilerArguments = argumentsByToolchain[selectedKey],
            BuildOutputDirectory = string.IsNullOrWhiteSpace(input.BuildOutputDirectory)
                ? Path.Combine(".cppeditor", "build")
                : input.BuildOutputDirectory.Trim(),
            CompilerArchivePath = string.Empty,
            GppPath = string.Empty,
            DebuggerPath = debuggerPath,
            GdbPath = debuggerPath
        };
    }

    private static string NormalizeDebuggerPath(string? debuggerPath, string? legacyGdbPath)
    {
        var raw = string.IsNullOrWhiteSpace(debuggerPath)
            ? legacyGdbPath
            : debuggerPath;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(raw.Trim().Trim('"'));
        }
        catch
        {
            return raw.Trim().Trim('"');
        }
    }

    private static bool ToolchainSectionEquals(ToolchainSettingsConfig? left, ToolchainSettingsConfig right)
    {
        if (left is null)
        {
            return false;
        }

        return string.Equals(left.SelectedToolchainId, right.SelectedToolchainId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.CompilerArguments, right.CompilerArguments, StringComparison.Ordinal) &&
               string.Equals(left.BuildOutputDirectory, right.BuildOutputDirectory, StringComparison.Ordinal) &&
               string.Equals(NormalizeDebuggerPath(left.DebuggerPath, left.GdbPath), right.DebuggerPath, StringComparison.OrdinalIgnoreCase) &&
               StringDictionaryEquals(left.ArgumentsByToolchain, right.ArgumentsByToolchain);
    }

    private static ToolchainId ResolveSelectedToolchainId(ToolchainSettingsConfig input)
    {
        if (ToolchainCatalog.TryParseId(input.SelectedToolchainId, out var parsed))
        {
            return parsed;
        }

        var legacyCompilerPath = (input.CompilerPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(legacyCompilerPath))
        {
            legacyCompilerPath = (input.GppPath ?? string.Empty).Trim();
        }

        var legacyRoot = (input.ToolchainRootPath ?? string.Empty).Trim();
        var normalizedCompiler = legacyCompilerPath.Replace('/', '\\');
        var normalizedRoot = legacyRoot.Replace('/', '\\');

        if (!string.IsNullOrWhiteSpace(normalizedCompiler))
        {
            if (normalizedCompiler.EndsWith(@"\clang++.exe", StringComparison.OrdinalIgnoreCase))
            {
                return ToolchainId.ClangPlusPlus;
            }

            if (normalizedCompiler.EndsWith(@"\clang.exe", StringComparison.OrdinalIgnoreCase))
            {
                return ToolchainId.Clang;
            }

            if (normalizedCompiler.EndsWith(@"\g++.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedCompiler.Contains(@"\mingw\", StringComparison.OrdinalIgnoreCase) &&
                    normalizedCompiler.Contains(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return ToolchainId.BuiltInMinGw;
                }

                return ToolchainId.Gpp;
            }

            if (normalizedCompiler.EndsWith(@"\gcc.exe", StringComparison.OrdinalIgnoreCase))
            {
                return ToolchainId.Gcc;
            }

            if (normalizedCompiler.EndsWith(@"\cl.exe", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedCompiler.Contains(@"\msvc\", StringComparison.OrdinalIgnoreCase) &&
                       normalizedCompiler.Contains(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase)
                    ? ToolchainId.BuiltInMsvc
                    : ToolchainId.LocalMsvc;
            }
        }

        if (normalizedRoot.Contains(@"\mingw", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRoot.Contains(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase)
                ? ToolchainId.BuiltInMinGw
                : ToolchainId.Gpp;
        }

        return ToolchainId.BuiltInMsvc;
    }

    private static string NormalizeToolchainArguments(ToolchainId id, string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return ToolchainCatalog.GetDefaultArguments(id);
        }

        if (id is ToolchainId.BuiltInMsvc or ToolchainId.LocalMsvc &&
            string.Equals(trimmed, "-std=c++17 -g", StringComparison.Ordinal))
        {
            return ToolchainCatalog.GetDefaultArguments(id);
        }

        if (id is ToolchainId.BuiltInMinGw or ToolchainId.Gcc or ToolchainId.Gpp or ToolchainId.Clang or ToolchainId.ClangPlusPlus &&
            string.Equals(trimmed, "/std:c++17 /EHsc /Zi /nologo", StringComparison.Ordinal))
        {
            return ToolchainCatalog.GetDefaultArguments(id);
        }

        return trimmed;
    }

    private static bool StringDictionaryEquals(
        Dictionary<string, string>? left,
        Dictionary<string, string>? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in right)
        {
            if (!left.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ExplorerSettingsConfig NormalizeExplorerSection(ExplorerSettingsConfig? section)
    {
        var input = section ?? new ExplorerSettingsConfig();
        return new ExplorerSettingsConfig
        {
            RenameSelectNameOnly = input.RenameSelectNameOnly
        };
    }

    private static bool ExplorerSectionEquals(ExplorerSettingsConfig? left, ExplorerSettingsConfig right)
    {
        if (left is null)
        {
            return false;
        }

        return left.RenameSelectNameOnly == right.RenameSelectNameOnly;
    }

    private static CppTemplateSettingsConfig NormalizeCppTemplateSection(CppTemplateSettingsConfig? section)
    {
        var input = section ?? CppTemplateSettingsConfig.CreateDefault();
        return new CppTemplateSettingsConfig
        {
            CppSourceTemplate = NormalizeTemplate(input.CppSourceTemplate, CppTemplateSettingsConfig.DefaultCppSourceTemplate),
            CppHeaderTemplate = NormalizeTemplate(input.CppHeaderTemplate, CppTemplateSettingsConfig.DefaultCppHeaderTemplate),
            CSourceTemplate = NormalizeTemplate(input.CSourceTemplate, CppTemplateSettingsConfig.DefaultCSourceTemplate),
            CHeaderTemplate = NormalizeTemplate(input.CHeaderTemplate, CppTemplateSettingsConfig.DefaultCHeaderTemplate),
            OtherFileTemplate = NormalizeTemplate(input.OtherFileTemplate, string.Empty)
        };
    }

    private static string NormalizeTemplate(string? template, string fallback)
    {
        return template is null
            ? fallback
            : template.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static bool CppTemplateSectionEquals(CppTemplateSettingsConfig? left, CppTemplateSettingsConfig right)
    {
        if (left is null)
        {
            return false;
        }

        return string.Equals(left.CppSourceTemplate, right.CppSourceTemplate, StringComparison.Ordinal) &&
               string.Equals(left.CppHeaderTemplate, right.CppHeaderTemplate, StringComparison.Ordinal) &&
               string.Equals(left.CSourceTemplate, right.CSourceTemplate, StringComparison.Ordinal) &&
               string.Equals(left.CHeaderTemplate, right.CHeaderTemplate, StringComparison.Ordinal) &&
               string.Equals(left.OtherFileTemplate, right.OtherFileTemplate, StringComparison.Ordinal);
    }

    private static BuildConfigurationSettings NormalizeBuildConfigurationSection(BuildConfigurationSettings? section)
    {
        var input = section ?? BuildConfigurationSettings.CreateDefault();
        
        // Ensure all toolchains have entries
        var debugArgs = input.DebugArgumentsByToolchain ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var releaseArgs = input.ReleaseArgumentsByToolchain ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var item in ToolchainCatalog.GetItems())
        {
            var key = ToolchainCatalog.ToConfigValue(item.Id);
            if (!debugArgs.ContainsKey(key))
            {
                debugArgs[key] = GetDefaultDebugArgsForToolchain(item.Id);
            }
            if (!releaseArgs.ContainsKey(key))
            {
                releaseArgs[key] = GetDefaultReleaseArgsForToolchain(item.Id);
            }
        }
        
        return new BuildConfigurationSettings
        {
            Configuration = input.Configuration,
            CustomConfigurationName = input.CustomConfigurationName ?? string.Empty,
            DebugArgumentsByToolchain = debugArgs,
            ReleaseArgumentsByToolchain = releaseArgs,
            CustomConfigurations = input.CustomConfigurations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string GetDefaultDebugArgsForToolchain(ToolchainId toolchainId)
    {
        return ToolchainCatalog.GetItem(toolchainId).Family == ToolchainFamily.Msvc
            ? "/std:c++17 /EHsc /Zi /nologo /Od /MDd"
            : "-std=c++17 -g -O0 -Wall";
    }

    private static string GetDefaultReleaseArgsForToolchain(ToolchainId toolchainId)
    {
        return ToolchainCatalog.GetItem(toolchainId).Family == ToolchainFamily.Msvc
            ? "/std:c++17 /EHsc /nologo /O2 /MD"
            : "-std=c++17 -O2 -Wall -DNDEBUG";
    }

    private static bool BuildConfigurationSectionEquals(BuildConfigurationSettings? left, BuildConfigurationSettings right)
    {
        if (left is null)
        {
            return false;
        }

        return left.Configuration == right.Configuration &&
               string.Equals(left.CustomConfigurationName, right.CustomConfigurationName, StringComparison.Ordinal);
    }

    private static CodeSnippetSettings NormalizeCodeSnippetSection(CodeSnippetSettings? section)
    {
        var input = section ?? CodeSnippetSettings.CreateDefault();
        return new CodeSnippetSettings
        {
            CustomSnippets = input.CustomSnippets ?? new List<CodeSnippet>(),
            EnableShortcutExpansion = input.EnableShortcutExpansion
        };
    }

    private static bool CodeSnippetSectionEquals(CodeSnippetSettings? left, CodeSnippetSettings right)
    {
        if (left is null)
        {
            return false;
        }

        return left.EnableShortcutExpansion == right.EnableShortcutExpansion;
    }

    private static CodeStructureSettings NormalizeCodeStructureSection(CodeStructureSettings? section)
    {
        var input = section ?? CodeStructureSettings.CreateDefault();
        return new CodeStructureSettings
        {
            ShowIncludes = input.ShowIncludes,
            ShowMacros = input.ShowMacros,
            ShowVariables = input.ShowVariables,
            SortAlphabetically = input.SortAlphabetically,
            AutoRefresh = input.AutoRefresh
        };
    }

    private static bool CodeStructureSectionEquals(CodeStructureSettings? left, CodeStructureSettings right)
    {
        if (left is null)
        {
            return false;
        }

        return left.ShowIncludes == right.ShowIncludes &&
               left.ShowMacros == right.ShowMacros &&
               left.ShowVariables == right.ShowVariables &&
               left.SortAlphabetically == right.SortAlphabetically &&
               left.AutoRefresh == right.AutoRefresh;
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

            var json = File.ReadAllText(path, Encoding.UTF8);
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

            File.WriteAllText(path, json, new UTF8Encoding(false));
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
    public int ConfigVersion { get; set; } = 10;

    public UiSettings Ui { get; set; } = new();

    public AutoPairSettingsConfig AutoPairs { get; set; } = new();

    public ShortcutSettingsSection Shortcuts { get; set; } = new();

    public ToolchainSettingsConfig Toolchain { get; set; } = ToolchainSettingsConfig.CreateDefault();

    public ExplorerSettingsConfig Explorer { get; set; } = new();

    public CppTemplateSettingsConfig CppTemplates { get; set; } = CppTemplateSettingsConfig.CreateDefault();

    public BuildConfigurationSettings BuildConfiguration { get; set; } = BuildConfigurationSettings.CreateDefault();

    public CodeSnippetSettings CodeSnippets { get; set; } = CodeSnippetSettings.CreateDefault();

    public CodeStructureSettings CodeStructure { get; set; } = CodeStructureSettings.CreateDefault();

    internal EditorAppConfig Clone()
    {
        return new EditorAppConfig
        {
            ConfigVersion = ConfigVersion,
            Ui = Ui?.Clone() ?? new UiSettings(),
            AutoPairs = AutoPairs?.Clone() ?? new AutoPairSettingsConfig(),
            Shortcuts = Shortcuts?.Clone() ?? new ShortcutSettingsSection(),
            Toolchain = Toolchain?.Clone() ?? ToolchainSettingsConfig.CreateDefault(),
            Explorer = Explorer?.Clone() ?? new ExplorerSettingsConfig(),
            CppTemplates = CppTemplates?.Clone() ?? CppTemplateSettingsConfig.CreateDefault(),
            BuildConfiguration = BuildConfiguration?.Clone() ?? BuildConfigurationSettings.CreateDefault(),
            CodeSnippets = CodeSnippets?.Clone() ?? CodeSnippetSettings.CreateDefault(),
            CodeStructure = CodeStructure?.Clone() ?? CodeStructureSettings.CreateDefault()
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

internal sealed class ToolchainSettingsConfig
{
    public string SelectedToolchainId { get; set; } = nameof(ToolchainId.BuiltInMsvc);

    public Dictionary<string, string> ArgumentsByToolchain { get; set; } = ToolchainCatalog.CreateDefaultArgumentsMap();

    public string CompilerPath { get; set; } = string.Empty;

    public string SetupScriptPath { get; set; } = string.Empty;

    // Legacy MinGW fields retained for config migration only.
    public string CompilerArchivePath { get; set; } = string.Empty;

    public string ToolchainRootPath { get; set; } = string.Empty;

    public string GppPath { get; set; } = string.Empty;

    public string DebuggerPath { get; set; } = string.Empty;

    public string GdbPath { get; set; } = string.Empty;

    public string CompilerArguments { get; set; } = "/std:c++17 /EHsc /Zi /nologo";

    public string BuildOutputDirectory { get; set; } = @".cppeditor\build";

    internal static ToolchainSettingsConfig CreateDefault()
    {
        return new ToolchainSettingsConfig
        {
            SelectedToolchainId = nameof(ToolchainId.BuiltInMsvc),
            ArgumentsByToolchain = ToolchainCatalog.CreateDefaultArgumentsMap(),
            CompilerPath = string.Empty,
            SetupScriptPath = string.Empty,
            CompilerArchivePath = string.Empty,
            ToolchainRootPath = string.Empty,
            GppPath = string.Empty,
            DebuggerPath = string.Empty,
            GdbPath = string.Empty,
            CompilerArguments = "/std:c++17 /EHsc /Zi /nologo",
            BuildOutputDirectory = @".cppeditor\build"
        };
    }

    internal ToolchainSettingsConfig Clone()
    {
        return new ToolchainSettingsConfig
        {
            SelectedToolchainId = SelectedToolchainId,
            ArgumentsByToolchain = new Dictionary<string, string>(
                ArgumentsByToolchain ?? ToolchainCatalog.CreateDefaultArgumentsMap(),
                StringComparer.OrdinalIgnoreCase),
            CompilerPath = CompilerPath,
            SetupScriptPath = SetupScriptPath,
            CompilerArchivePath = CompilerArchivePath,
            ToolchainRootPath = ToolchainRootPath,
            GppPath = GppPath,
            DebuggerPath = string.IsNullOrWhiteSpace(DebuggerPath) ? GdbPath : DebuggerPath,
            GdbPath = GdbPath,
            CompilerArguments = CompilerArguments,
            BuildOutputDirectory = BuildOutputDirectory
        };
    }
}

internal sealed class ExplorerSettingsConfig
{
    public bool RenameSelectNameOnly { get; set; } = true;

    internal ExplorerSettingsConfig Clone()
    {
        return new ExplorerSettingsConfig
        {
            RenameSelectNameOnly = RenameSelectNameOnly
        };
    }
}

internal sealed class CppTemplateSettingsConfig
{
    internal const string DefaultCppSourceTemplate =
        "#include <iostream>\n\nint main()\n{\n    std::cout << \"Hello, World!\" << std::endl;\n    return 0;\n}\n";

    internal const string DefaultCppHeaderTemplate =
        "#pragma once\n\n";

    internal const string DefaultCSourceTemplate =
        "#include <stdio.h>\n\nint main(void)\n{\n    printf(\"Hello, World!\\n\");\n    return 0;\n}\n";

    internal const string DefaultCHeaderTemplate =
        "#pragma once\n\n";

    public string CppSourceTemplate { get; set; } = DefaultCppSourceTemplate;

    public string CppHeaderTemplate { get; set; } = DefaultCppHeaderTemplate;

    public string CSourceTemplate { get; set; } = DefaultCSourceTemplate;

    public string CHeaderTemplate { get; set; } = DefaultCHeaderTemplate;

    public string OtherFileTemplate { get; set; } = string.Empty;

    internal static CppTemplateSettingsConfig CreateDefault()
    {
        return new CppTemplateSettingsConfig
        {
            CppSourceTemplate = DefaultCppSourceTemplate,
            CppHeaderTemplate = DefaultCppHeaderTemplate,
            CSourceTemplate = DefaultCSourceTemplate,
            CHeaderTemplate = DefaultCHeaderTemplate,
            OtherFileTemplate = string.Empty
        };
    }

    internal CppTemplateSettingsConfig Clone()
    {
        return new CppTemplateSettingsConfig
        {
            CppSourceTemplate = CppSourceTemplate,
            CppHeaderTemplate = CppHeaderTemplate,
            CSourceTemplate = CSourceTemplate,
            CHeaderTemplate = CHeaderTemplate,
            OtherFileTemplate = OtherFileTemplate
        };
    }
}

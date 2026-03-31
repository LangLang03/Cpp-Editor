namespace C__Editor;

internal sealed class ToolchainResolver
{
    private readonly ToolchainDiscoveryService discoveryService;

    internal ToolchainResolver(ToolchainDiscoveryService discoveryService)
    {
        this.discoveryService = discoveryService;
    }

    internal IReadOnlyList<ToolchainProbeResult> DiscoverCandidates()
    {
        return discoveryService.Discover();
    }

    internal ToolchainId GetSelectedToolchainId(ToolchainSettingsConfig? settings)
    {
        return ToolchainCatalog.ParseOrDefault(settings?.SelectedToolchainId);
    }

    internal Dictionary<ToolchainId, string> GetArgumentsByToolchain(ToolchainSettingsConfig? settings)
    {
        var selectedId = GetSelectedToolchainId(settings);
        var result = new Dictionary<ToolchainId, string>();

        foreach (var item in ToolchainCatalog.GetItems())
        {
            result[item.Id] = ToolchainCatalog.GetDefaultArguments(item.Id);
        }

        if (settings?.ArgumentsByToolchain is not null)
        {
            foreach (var pair in settings.ArgumentsByToolchain)
            {
                if (!ToolchainCatalog.TryParseId(pair.Key, out var id))
                {
                    continue;
                }

                var normalized = NormalizeArguments(id, pair.Value);
                result[id] = normalized;
            }
        }

        var legacy = NormalizeArguments(selectedId, settings?.CompilerArguments);
        if (string.IsNullOrWhiteSpace(result[selectedId]))
        {
            result[selectedId] = legacy;
        }

        return result;
    }

    internal bool TryResolveSelected(
        ToolchainSettingsConfig? settings,
        out ResolvedToolchainContext context,
        out string detail)
    {
        context = new ResolvedToolchainContext();
        var selectedId = GetSelectedToolchainId(settings);
        var candidates = DiscoverCandidates();
        var selectedCandidate = candidates.FirstOrDefault(item => item.Id == selectedId);
        if (selectedCandidate is null)
        {
            detail = $"未识别的工具链: {selectedId}";
            return false;
        }

        if (!selectedCandidate.IsAvailable)
        {
            var unavailableReason = string.IsNullOrWhiteSpace(selectedCandidate.UnavailableReason)
                ? "工具链不可用。"
                : selectedCandidate.UnavailableReason;
            detail = $"当前选择 `{selectedCandidate.DisplayName}` 不可用：{unavailableReason}\r\n请打开“工具 -> 编译器设置”切换可用工具链。";
            return false;
        }

        var argsMap = GetArgumentsByToolchain(settings);
        var compilerArguments = argsMap.TryGetValue(selectedId, out var value)
            ? NormalizeArguments(selectedId, value)
            : ToolchainCatalog.GetDefaultArguments(selectedId);

        context = new ResolvedToolchainContext
        {
            Id = selectedCandidate.Id,
            Family = selectedCandidate.Family,
            DisplayName = selectedCandidate.DisplayName,
            CompilerPath = selectedCandidate.CompilerPath,
            SetupScriptPath = selectedCandidate.SetupScriptPath,
            ToolchainRootPath = selectedCandidate.ToolchainRootPath,
            CompilerArguments = compilerArguments,
            Source = selectedCandidate.Source
        };

        detail = string.IsNullOrWhiteSpace(selectedCandidate.Detail)
            ? $"已选择工具链: {selectedCandidate.DisplayName}"
            : selectedCandidate.Detail;
        return true;
    }

    private static string NormalizeArguments(ToolchainId id, string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
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
}

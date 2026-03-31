namespace C__Editor;

internal enum ToolchainId
{
    BuiltInMsvc,
    LocalMsvc,
    BuiltInMinGw,
    Gcc,
    Gpp,
    Clang,
    ClangPlusPlus
}

internal enum ToolchainFamily
{
    Msvc,
    GnuLike
}

internal sealed class ToolchainCatalogItem
{
    public ToolchainCatalogItem(ToolchainId id, ToolchainFamily family, string displayName)
    {
        Id = id;
        Family = family;
        DisplayName = displayName;
    }

    public ToolchainId Id { get; }

    public ToolchainFamily Family { get; }

    public string DisplayName { get; }
}

internal static class ToolchainCatalog
{
    private static readonly IReadOnlyList<ToolchainCatalogItem> Items = new[]
    {
        new ToolchainCatalogItem(ToolchainId.BuiltInMsvc, ToolchainFamily.Msvc, "内置 MSVC"),
        new ToolchainCatalogItem(ToolchainId.LocalMsvc, ToolchainFamily.Msvc, "本地 MSVC"),
        new ToolchainCatalogItem(ToolchainId.BuiltInMinGw, ToolchainFamily.GnuLike, "内置 MinGW"),
        new ToolchainCatalogItem(ToolchainId.Gcc, ToolchainFamily.GnuLike, "gcc"),
        new ToolchainCatalogItem(ToolchainId.Gpp, ToolchainFamily.GnuLike, "g++"),
        new ToolchainCatalogItem(ToolchainId.Clang, ToolchainFamily.GnuLike, "clang"),
        new ToolchainCatalogItem(ToolchainId.ClangPlusPlus, ToolchainFamily.GnuLike, "clang++")
    };

    private static readonly IReadOnlyDictionary<ToolchainId, ToolchainCatalogItem> ItemsById =
        Items.ToDictionary(item => item.Id);

    internal static IReadOnlyList<ToolchainCatalogItem> GetItems()
    {
        return Items;
    }

    internal static ToolchainCatalogItem GetItem(ToolchainId id)
    {
        return ItemsById[id];
    }

    internal static string ToConfigValue(ToolchainId id)
    {
        return id.ToString();
    }

    internal static bool TryParseId(string? raw, out ToolchainId id)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            Enum.TryParse(raw.Trim(), ignoreCase: true, out id))
        {
            return true;
        }

        id = default;
        return false;
    }

    internal static ToolchainId ParseOrDefault(string? raw, ToolchainId fallback = ToolchainId.BuiltInMsvc)
    {
        return TryParseId(raw, out var parsed) ? parsed : fallback;
    }

    internal static string GetDefaultArguments(ToolchainId id)
    {
        return GetItem(id).Family == ToolchainFamily.Msvc
            ? "/std:c++17 /EHsc /Zi /nologo"
            : "-std=c++17 -g -fdiagnostics-color=never";
    }

    internal static Dictionary<string, string> CreateDefaultArgumentsMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items)
        {
            map[ToConfigValue(item.Id)] = GetDefaultArguments(item.Id);
        }

        return map;
    }
}

internal sealed class ToolchainProbeHit
{
    public ToolchainId Id { get; init; }

    public string CompilerPath { get; init; } = string.Empty;

    public string SetupScriptPath { get; init; } = string.Empty;

    public string ToolchainRootPath { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

internal sealed class ToolchainProbeResult
{
    public ToolchainId Id { get; init; }

    public ToolchainFamily Family { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public bool IsAvailable { get; init; }

    public string UnavailableReason { get; init; } = string.Empty;

    public string CompilerPath { get; init; } = string.Empty;

    public string SetupScriptPath { get; init; } = string.Empty;

    public string ToolchainRootPath { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

internal sealed class ResolvedToolchainContext
{
    public ToolchainId Id { get; init; }

    public ToolchainFamily Family { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string CompilerPath { get; init; } = string.Empty;

    public string SetupScriptPath { get; init; } = string.Empty;

    public string ToolchainRootPath { get; init; } = string.Empty;

    public string CompilerArguments { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;
}

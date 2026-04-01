using System.Security.Cryptography;
using System.Text;

namespace C__Editor;

internal sealed class CodeStructureAnalyzer
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    internal CodeStructureParseResult Analyze(string filePath, string content, bool forceRefresh = false)
    {
        var normalizedPath = NormalizePath(filePath);
        var safeContent = content ?? string.Empty;
        var fingerprint = ComputeFingerprint(safeContent);

        lock (syncRoot)
        {
            if (!forceRefresh &&
                cache.TryGetValue(normalizedPath, out var cached) &&
                string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cached.Result.Clone();
            }
        }

        var parsed = ParseContent(normalizedPath, safeContent);

        lock (syncRoot)
        {
            cache[normalizedPath] = new CacheEntry(fingerprint, parsed.Clone());
        }

        return parsed;
    }

    internal CodeStructureParseResult AnalyzeFile(string filePath, bool forceRefresh = false)
    {
        var normalizedPath = NormalizePath(filePath);
        if (!File.Exists(normalizedPath))
        {
            return new CodeStructureParseResult
            {
                FilePath = normalizedPath,
                ParseTime = DateTime.Now,
                IsPartial = true,
                ErrorMessage = "File not found"
            };
        }

        var content = File.ReadAllText(normalizedPath);
        return Analyze(normalizedPath, content, forceRefresh);
    }

    internal bool TryGetCachedResult(string filePath, out CodeStructureParseResult? result)
    {
        var normalizedPath = NormalizePath(filePath);
        lock (syncRoot)
        {
            if (cache.TryGetValue(normalizedPath, out var entry))
            {
                result = entry.Result.Clone();
                return true;
            }
        }

        result = null;
        return false;
    }

    internal void Invalidate(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        lock (syncRoot)
        {
            cache.Remove(normalizedPath);
        }
    }

    private static CodeStructureParseResult ParseContent(string filePath, string content)
    {
        try
        {
            var lexer = new CppLexer(content);
            var tokens = lexer.Tokenize();
            var parser = new CppParser(filePath, tokens);
            return parser.Parse();
        }
        catch (Exception ex)
        {
            return new CodeStructureParseResult
            {
                FilePath = filePath,
                ParseTime = DateTime.Now,
                IsPartial = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string NormalizePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch
        {
            return filePath;
        }
    }

    private static string ComputeFingerprint(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private readonly record struct CacheEntry(string Fingerprint, CodeStructureParseResult Result);
}

using System.Text;

namespace C__Editor;

internal static class EditorFileEncodingHelper
{
    internal readonly record struct EncodingOption(string Label, Encoding Encoding);

    internal readonly record struct FileReadResult(string Text, Encoding Encoding, string DisplayName);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16Le = new(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding Utf16Be = new(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);

    private static readonly IReadOnlyList<EncodingOption> CommonEncodings =
    [
        new EncodingOption("UTF-8", new UTF8Encoding(false)),
        new EncodingOption("UTF-8 BOM", new UTF8Encoding(true)),
        new EncodingOption("GB18030", Encoding.GetEncoding("GB18030")),
        new EncodingOption("GB2312", Encoding.GetEncoding("GB2312")),
        new EncodingOption("Big5", Encoding.GetEncoding("big5")),
        new EncodingOption("UTF-16 LE", new UnicodeEncoding(false, true)),
        new EncodingOption("UTF-16 BE", new UnicodeEncoding(true, true)),
        new EncodingOption("Windows-1252", Encoding.GetEncoding(1252))
    ];

    static EditorFileEncodingHelper()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    internal static Encoding DefaultEncoding => new UTF8Encoding(false);

    internal static IReadOnlyList<EncodingOption> GetCommonEncodings()
    {
        return CommonEncodings;
    }

    internal static FileReadResult ReadFileWithDetectedEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return new FileReadResult(DecodeStrict(bytes, Utf8Bom, 3), new UTF8Encoding(true), "UTF-8 BOM");
        }

        if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            var encoding = new UTF32Encoding(false, true, true);
            return new FileReadResult(DecodeStrict(bytes, encoding, 4), encoding, "UTF-32 LE");
        }

        if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            var encoding = new UTF32Encoding(true, true, true);
            return new FileReadResult(DecodeStrict(bytes, encoding, 4), encoding, "UTF-32 BE");
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return new FileReadResult(DecodeStrict(bytes, Utf16Le, 2), new UnicodeEncoding(false, true), "UTF-16 LE");
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return new FileReadResult(DecodeStrict(bytes, Utf16Be, 2), new UnicodeEncoding(true, true), "UTF-16 BE");
        }

        try
        {
            return new FileReadResult(DecodeStrict(bytes, Utf8NoBom), new UTF8Encoding(false), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            var gb18030 = Encoding.GetEncoding("GB18030");
            return new FileReadResult(gb18030.GetString(bytes), gb18030, "GB18030");
        }
    }

    internal static FileReadResult ReadFileWithEncoding(string filePath, Encoding encoding, string? displayName = null)
    {
        var bytes = File.ReadAllBytes(filePath);
        var offset = GetPreambleLength(bytes, encoding.GetPreamble());
        var text = encoding.GetString(bytes, offset, bytes.Length - offset);
        var label = string.IsNullOrWhiteSpace(displayName) ? GetDisplayName(encoding) : displayName!;
        return new FileReadResult(text, encoding, label);
    }

    internal static string GetDisplayName(Encoding encoding)
    {
        if (encoding is UTF8Encoding utf8)
        {
            return utf8.GetPreamble().Length > 0 ? "UTF-8 BOM" : "UTF-8";
        }

        if (encoding is UnicodeEncoding unicode)
        {
            if (unicode.CodePage == 1201)
            {
                return "UTF-16 BE";
            }

            return "UTF-16 LE";
        }

        if (encoding.WebName.Equals("gb18030", StringComparison.OrdinalIgnoreCase))
        {
            return "GB18030";
        }

        if (encoding.WebName.Equals("gb2312", StringComparison.OrdinalIgnoreCase))
        {
            return "GB2312";
        }

        if (encoding.WebName.Equals("big5", StringComparison.OrdinalIgnoreCase))
        {
            return "Big5";
        }

        return string.IsNullOrWhiteSpace(encoding.WebName)
            ? $"CodePage {encoding.CodePage}"
            : encoding.WebName.ToUpperInvariant();
    }

    private static string DecodeStrict(byte[] bytes, Encoding encoding, int offset = 0)
    {
        var start = Math.Clamp(offset, 0, bytes.Length);
        var strict = Encoding.GetEncoding(
            encoding.CodePage,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);

        return strict.GetString(bytes, start, bytes.Length - start);
    }

    private static bool HasPrefix(IReadOnlyList<byte> bytes, params byte[] prefix)
    {
        if (bytes.Count < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int GetPreambleLength(IReadOnlyList<byte> bytes, IReadOnlyList<byte> preamble)
    {
        if (preamble.Count == 0 || bytes.Count < preamble.Count)
        {
            return 0;
        }

        for (var i = 0; i < preamble.Count; i++)
        {
            if (bytes[i] != preamble[i])
            {
                return 0;
            }
        }

        return preamble.Count;
    }
}

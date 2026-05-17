namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Server-side MIME sniffer — inspects the leading magic bytes of an
/// upload to derive its real type. <b>The filename extension is not
/// trusted</b>; a renamed <c>shell.sh</c> claiming to be <c>resume.pdf</c>
/// gets sniffed as <c>text/x-shellscript</c> (or falls through to
/// <c>application/octet-stream</c>, which the policy rejects).
///
/// <para>
/// This is a small, focused signature table — it does NOT aspire to be
/// libmagic. It covers the MIMEs in <see cref="DefaultMimeWhitelist"/>
/// plus a few common attackers ("PE header", "ELF", "shell shebang")
/// that the policy must specifically reject.
/// </para>
///
/// <para>
/// <b>Council review note:</b> if a host's tenant whitelist allows a
/// MIME this sniffer can't detect (e.g., <c>application/json</c> which
/// has no fixed magic), `Sniff` returns
/// <see cref="UnknownMime"/> for that payload. The policy treats
/// <see cref="UnknownMime"/> as an inconclusive sniff and rejects the
/// upload unless the tenant explicitly whitelists <see cref="UnknownMime"/>.
/// This is intentional defense-in-depth: detection failure is treated
/// as deny, not allow. The trade-off is some legitimate uploads
/// (text/csv, application/json) require an explicit per-tenant allow.
/// </para>
/// </summary>
public static class MimeSniffer
{
    /// <summary>The sentinel returned when no signature matched.</summary>
    public const string UnknownMime = "application/octet-stream";

    private static readonly byte[] PngMagic =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegMagic =
        { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] ZipMagic =
        { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] ElfMagic =
        { 0x7F, 0x45, 0x4C, 0x46 };

    /// <summary>
    /// Sniff the canonical MIME type from leading magic bytes.
    ///
    /// <para>
    /// <b>Council doc-amendment (council A).</b> This sniffer reads at most
    /// the first 512 bytes of <paramref name="bytes"/> (the SVG / HTML
    /// prefix scan is the longest path). A successful sniff confirms the
    /// CONTAINER FORMAT of the leading bytes only; it does NOT certify
    /// the entire payload is safe. Polyglot files (a JPEG with a ZIP
    /// trailer; a PDF carrying embedded JavaScript) are intentionally
    /// out-of-scope for this layer — they're sniffed as the leading
    /// container and the downstream consumer is responsible for any
    /// deeper inspection (e.g., PDF script-stripping, OOXML macro-policy).
    /// </para>
    /// </summary>
    public static string Sniff(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return UnknownMime;

        // PDF: "%PDF-"
        if (StartsWith(bytes, "%PDF-"u8)) return "application/pdf";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        // (u8 literals UTF-8-encode codepoints ≥ 0x80, so the high-bit signature
        // bytes here are kept as a literal byte array.)
        if (StartsWith(bytes, PngMagic)) return "image/png";

        // JPEG: FF D8 FF
        if (StartsWith(bytes, JpegMagic)) return "image/jpeg";

        // GIF (legacy; not in default whitelist but recognized for accurate sniffing)
        if (StartsWith(bytes, "GIF87a"u8) || StartsWith(bytes, "GIF89a"u8)) return "image/gif";

        // WEBP: "RIFF????WEBP"
        if (bytes.Length >= 12 && StartsWith(bytes, "RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";

        // HEIC/HEIF: "ftypheic" or "ftypheix" or "ftypmif1" at offset 4
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = bytes.Slice(8, 4);
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8)
                || brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("heim"u8))
                return "image/heic";
        }

        // SVG: starts with "<svg" or "<?xml" followed by "<svg" (in first 256 bytes)
        if (LooksLikeSvg(bytes)) return "image/svg+xml";

        // ZIP / OOXML: "PK\x03\x04". Office Open XML (docx/xlsx) is a ZIP — sniff the
        // first entry filename to distinguish, but for v1 we return application/zip
        // and let the caller's whitelist decide; tenants that want docx/xlsx must
        // also whitelist application/zip (or refine via a richer sniffer in a v2 pass).
        if (StartsWith(bytes, ZipMagic)) return "application/zip";

        // PE / Windows executable: "MZ"
        if (StartsWith(bytes, "MZ"u8)) return "application/x-msdownload";

        // ELF
        if (StartsWith(bytes, ElfMagic)) return "application/x-executable";

        // Shell shebang
        if (StartsWith(bytes, "#!"u8)) return "text/x-shellscript";

        // HTML — sniff for "<!doctype html" or "<html" in first 512 bytes
        if (LooksLikeHtml(bytes)) return "text/html";

        return UnknownMime;
    }

    private static bool StartsWith(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle) =>
        haystack.Length >= needle.Length && haystack.Slice(0, needle.Length).SequenceEqual(needle);

    private static bool LooksLikeSvg(ReadOnlySpan<byte> bytes)
    {
        var prefix = bytes.Slice(0, Math.Min(bytes.Length, 256));
        // Lowercase ASCII comparison, ignoring whitespace.
        Span<char> chars = stackalloc char[prefix.Length];
        for (int i = 0; i < prefix.Length; i++) chars[i] = char.ToLowerInvariant((char)prefix[i]);
        var s = new string(chars);
        return s.Contains("<svg", StringComparison.Ordinal);
    }

    private static bool LooksLikeHtml(ReadOnlySpan<byte> bytes)
    {
        var prefix = bytes.Slice(0, Math.Min(bytes.Length, 512));
        Span<char> chars = stackalloc char[prefix.Length];
        for (int i = 0; i < prefix.Length; i++) chars[i] = char.ToLowerInvariant((char)prefix[i]);
        var s = new string(chars);
        return s.Contains("<!doctype html", StringComparison.Ordinal)
            || s.Contains("<html", StringComparison.Ordinal);
    }
}

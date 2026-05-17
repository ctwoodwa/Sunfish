namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Path-traversal defense for the <c>OriginalFilename</c> field. Strips
/// directory components, rejects control characters, and refuses Windows
/// reserved device names. Returns <c>null</c> for any input the sanitizer
/// would prefer to reject; the service treats null as a rejection and
/// substitutes a safe fallback (the attachment's content-hash-derived
/// name).
///
/// <para>
/// <b>Council review focus:</b> this sanitizer is the only barrier
/// between user-controlled filename input and any downstream consumer
/// that might render or persist that filename (e.g., a sync surface
/// writing it to a filesystem with the actual blob). Any failure mode
/// in here is a defense-in-depth gap.
/// </para>
/// </summary>
public static class FilenameSanitizer
{
    private static readonly char[] DirSeparators = { '/', '\\', ':' };
    private static readonly char[] ControlChars = Enumerable.Range(0, 32).Select(i => (char)i).ToArray();

    // Windows reserved device names — case-insensitive, with or without extension.
    private static readonly HashSet<string> WindowsReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Unicode bidi-override and zero-width chars — display-name spoofing
    // (council hardening). RTL override makes "evilexe.txt" render as "eviltxt.exe".
    private static readonly HashSet<char> BidiAndZeroWidth = new()
    {
        '‪', '‫', '‬', '‭', '‮', // bidi embed/override/pop
        '⁦', '⁧', '⁨', '⁩',           // bidi isolate / pop
        '​', '‌', '‍', '﻿',           // zero-width / BOM
    };

    /// <summary>
    /// Returns the sanitized leaf filename, or <c>null</c> if no safe
    /// form can be derived.
    ///
    /// <para>
    /// <b>Caller contract (SE-5, council-mandated).</b> Callers MUST
    /// pre-normalize the raw filename: URL-decode any percent-encoding
    /// and apply Unicode NFC normalization to fold confusables. This
    /// sanitizer operates on the post-normalization form and rejects
    /// raw <c>/</c>, <c>\</c>, <c>:</c> in any normalization. Downstream
    /// consumers MUST NOT re-decode the output — the sanitized leaf is
    /// the contract boundary; treating it as still-encoded re-introduces
    /// the traversal vector this layer just closed.
    /// </para>
    /// </summary>
    public static string? Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1. Strip any path components — keep only the leaf.
        var leaf = raw;
        var lastSep = raw.LastIndexOfAny(DirSeparators);
        if (lastSep >= 0) leaf = raw.Substring(lastSep + 1);
        if (string.IsNullOrEmpty(leaf)) return null;

        // 2. Reject if any control char is present.
        if (leaf.IndexOfAny(ControlChars) >= 0) return null;

        // 3. Strip bidi-override and zero-width chars (display-name spoofing).
        if (leaf.Any(c => BidiAndZeroWidth.Contains(c)))
        {
            leaf = new string(leaf.Where(c => !BidiAndZeroWidth.Contains(c)).ToArray());
            if (string.IsNullOrEmpty(leaf)) return null;
        }

        // 4. Trim leading/trailing whitespace AND dots BEFORE the reserved-name
        // check (SE-3 council blocker). Windows strips trailing space/dot when
        // resolving a path, so "CON " resolves to "CON". Doing the trim AFTER
        // the reserved-name check let "CON " sneak past the HashSet lookup.
        leaf = leaf.Trim(' ', '.');
        if (string.IsNullOrEmpty(leaf)) return null;

        // 5. Reject the special directory names (after trim — "..."/".." both empty out).
        if (leaf is "." or "..") return null;

        // 6. Reject Windows reserved device names. Split on the FIRST `.` rather
        // than the last (SE-3 council blocker) — Windows resolves "COM1.foo.bar"
        // against "COM1", not "COM1.foo".
        var firstDot = leaf.IndexOf('.');
        var stem = firstDot >= 0 ? leaf.Substring(0, firstDot) : leaf;
        // Stem may carry residual trailing whitespace if a multi-dot filename
        // had whitespace before its first dot (e.g., "CON .pdf" → stem = "CON ").
        // Trim it before the reserved-name check.
        stem = stem.Trim(' ', '.');
        if (WindowsReserved.Contains(stem)) return null;

        // 7. Length cap — prevent absurd filenames overflowing downstream UIs / filesystems.
        const int maxLen = 255;
        if (leaf.Length > maxLen) leaf = leaf.Substring(0, maxLen);

        return leaf;
    }
}

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

    /// <summary>
    /// Returns the sanitized leaf filename, or <c>null</c> if no safe
    /// form can be derived.
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

        // 3. Reject the special directory names.
        if (leaf is "." or "..") return null;

        // 4. Reject Windows reserved device names (no extension and with extension).
        var withoutExt = System.IO.Path.GetFileNameWithoutExtension(leaf);
        if (WindowsReserved.Contains(withoutExt)) return null;

        // 5. Trim trailing whitespace and dots (Windows strips these on disk).
        leaf = leaf.TrimEnd(' ', '.');
        if (string.IsNullOrEmpty(leaf)) return null;

        // 6. Length cap — prevent absurd filenames overflowing downstream UIs / filesystems.
        const int maxLen = 255;
        if (leaf.Length > maxLen) leaf = leaf.Substring(0, maxLen);

        return leaf;
    }
}

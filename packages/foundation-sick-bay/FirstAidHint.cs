using System;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Contextual help hint surfaced via <see cref="IFirstAidSurface"/> per
/// ADR 0082 §4. <see cref="Body"/> is plain-text-validated at construction
/// so a downstream renderer cannot inject HTML / script content from a
/// hint registered through the open <see cref="SickBayOptions"/>
/// configuration channel.
/// </summary>
/// <remarks>
/// <b>§Trust impact:</b> the constructor REJECTS strings containing
/// HTML metacharacters (<c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>) or ASCII
/// control chars below 0x20 except line-feed (<c>\n</c>). The resulting
/// hint is safe to render verbatim with no per-renderer escaping. Per
/// W#54 P1 council Minor m4: rejecting <c>&amp;</c> bars legitimate
/// plain-text strings such as "Q&amp;A"; callers needing such glyphs
/// SHOULD split the body into two hints OR wait for the Phase 2
/// permit-and-pre-escape mode that is planned to honor a dedicated
/// safe-rendering wrapper.
/// </remarks>
public sealed record FirstAidHint
{
    /// <summary>Stable kebab-case key for the hint (e.g., <c>"sick-bay.medevac.four-eyes"</c>).</summary>
    public string Key { get; }

    /// <summary>Localized title (rendered as the hint heading).</summary>
    public string Title { get; }

    /// <summary>Plain-text body; validated against HTML/control-char injection.</summary>
    public string Body { get; }

    /// <summary>Severity discriminator.</summary>
    public FirstAidLevel Level { get; }

    /// <summary>Constructs a hint, validating <paramref name="Body"/> against the §Trust plain-text rules.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="Body"/> contains HTML metacharacters or non-LF control chars.</exception>
    public FirstAidHint(string Key, string Title, string Body, FirstAidLevel Level)
    {
        ArgumentNullException.ThrowIfNull(Key);
        ArgumentNullException.ThrowIfNull(Title);
        ArgumentNullException.ThrowIfNull(Body);
        ValidatePlainText(Body, nameof(Body));

        this.Key = Key;
        this.Title = Title;
        this.Body = Body;
        this.Level = Level;
    }

    private static void ValidatePlainText(string value, string paramName)
    {
        foreach (var ch in value)
        {
            if (ch is '<' or '>' or '&')
            {
                throw new ArgumentException(
                    $"{paramName} contains HTML metacharacter '{ch}'; FirstAidHint.Body MUST be plain text.",
                    paramName);
            }
            if (ch < 0x20 && ch != '\n')
            {
                throw new ArgumentException(
                    $"{paramName} contains control character U+{(int)ch:X4}; only line-feed is permitted.",
                    paramName);
            }
        }
    }
}

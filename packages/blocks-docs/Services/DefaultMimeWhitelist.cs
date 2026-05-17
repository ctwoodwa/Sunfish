namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Default MIME-type whitelist used when a tenant has no per-tenant
/// override. Deny-by-default posture: anything not in this list is
/// rejected by <see cref="IMimeTypeAndSizePolicy"/>.
///
/// <para>
/// <b>Excluded by design</b> (defense-in-depth) and ALSO enforced as a
/// system blacklist (see <see cref="SystemBlacklist"/>) so a tenant
/// whitelist override CANNOT re-enable these:
/// </para>
/// <list type="bullet">
/// <item><c>application/x-msdownload</c>, <c>application/x-executable</c>, <c>application/x-sh</c> — no executables.</item>
/// <item><c>application/octet-stream</c> — the sniffer falls back to this when it can't recognize content; policy rejects so unknown content can't sneak through.</item>
/// <item><c>text/html</c>, <c>application/javascript</c>, <c>text/javascript</c> — no live web content (XSS risk if rendered in-app).</item>
/// <item><c>application/x-shockwave-flash</c> — Flash deprecated.</item>
/// </list>
///
/// <para>
/// <b>ZIP and OOXML caveat (council doc-amendment).</b> Whitelisting
/// <c>application/zip</c> implicitly admits any ZIP-based container:
/// docx, xlsx, pptx, odt, jar, apk. The sniffer returns
/// <c>application/zip</c> for all of these; the deeper sniff that
/// distinguishes OOXML by reading the central directory is deferred to
/// a v2 follow-on (<c>blocks-docs-ooxml-deep-sniff-followon</c>).
/// Downstream consumers that decompress ZIP/OOXML MUST enforce an
/// uncompressed-size cap; this substrate persists the ZIP bytes as-is
/// and does not decompress.
/// </para>
///
/// <para>
/// Tenants that need a different policy (e.g., a tenant uploading code
/// snippets that need JavaScript MIME) set
/// <see cref="Models.BlocksDocsOptions.MimeWhitelistPerTenant"/> — but
/// MIMEs in <see cref="SystemBlacklist"/> are unaffected by that override.
/// </para>
/// </summary>
public static class DefaultMimeWhitelist
{
    /// <summary>The default whitelist, immutable, case-insensitive comparison.</summary>
    public static readonly IReadOnlySet<string> Defaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/json",

        // Images (inspection photos, marketing DAM v1)
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/svg+xml",

        // Compressed (uncommon; expected for migration imports only).
        // NOTE: also admits docx/xlsx/pptx/odt/jar/apk — see class remarks.
        "application/zip",
    };

    /// <summary>
    /// MIMEs that NO tenant whitelist may re-enable (SE-2 council blocker).
    /// Enforced as a pre-gate in
    /// <see cref="MimeTypeAndSizePolicy.ValidateAsync"/> ahead of the
    /// per-tenant whitelist lookup — so a misconfigured host or
    /// future tenant-config UI cannot accidentally re-enable a stored-XSS
    /// or executable-upload vector.
    /// </summary>
    public static readonly IReadOnlySet<string> SystemBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "application/javascript",
        "text/javascript",
        "application/x-msdownload",
        "application/x-executable",
        "application/x-sh",
        "application/octet-stream",
        "application/x-shockwave-flash",
    };
}

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Default MIME-type whitelist used when a tenant has no per-tenant
/// override. Deny-by-default posture: anything not in this list is
/// rejected by <see cref="IMimeTypeAndSizePolicy"/>.
///
/// <para>
/// <b>Excluded by design</b> (defense-in-depth):
/// </para>
/// <list type="bullet">
/// <item><c>application/x-msdownload</c>, <c>application/x-executable</c>, <c>application/x-sh</c> — no executables.</item>
/// <item><c>application/octet-stream</c> — the sniffer falls back to this when it can't recognize content; policy rejects so unknown content can't sneak through.</item>
/// <item><c>text/html</c>, <c>application/javascript</c>, <c>text/javascript</c> — no live web content (XSS risk if rendered in-app).</item>
/// <item><c>application/x-shockwave-flash</c> — Flash deprecated.</item>
/// </list>
///
/// <para>
/// Tenants that need a different policy (e.g., a tenant uploading code
/// snippets that need JavaScript MIME) set
/// <see cref="Models.BlocksDocsOptions.MimeWhitelistPerTenant"/>.
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

        // Compressed (uncommon; expected for migration imports only)
        "application/zip",
    };
}

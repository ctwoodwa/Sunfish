namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// Host-supplied configuration for the docs cluster. Wired through
/// <see cref="DependencyInjection.DocsServiceCollectionExtensions.AddBlocksDocs"/>.
///
/// <para>
/// <b>Defense-in-depth posture.</b> Every policy field has a
/// conservative default — a host that calls
/// <c>AddBlocksDocs()</c> with no configuration gets the safest
/// behavior automatically (whitelisted MIMEs only, 100 MB per-attachment
/// cap, no per-tenant cumulative quota — flag it on per tenant when
/// hosting agents want to enforce one).
/// </para>
/// </summary>
public sealed class BlocksDocsOptions
{
    /// <summary>
    /// Per-attachment cap in bytes. Default 100 MB. Per-tenant overrides
    /// live in <see cref="MaxAttachmentBytesPerTenant"/>.
    /// </summary>
    public long MaxAttachmentBytes { get; init; } = 100L * 1024 * 1024;

    /// <summary>
    /// Per-tenant override for <see cref="MaxAttachmentBytes"/>. Set a
    /// tenant's value lower for stricter hosts, higher for hosts with
    /// known-large attachment use cases.
    /// </summary>
    public IReadOnlyDictionary<string, long> MaxAttachmentBytesPerTenant { get; init; }
        = new Dictionary<string, long>();

    /// <summary>
    /// Per-tenant cumulative quota in bytes. <c>null</c> = unlimited.
    /// Default: empty dictionary (no quotas). Hosting agents set this per
    /// tenant for cost / storage discipline.
    /// </summary>
    public IReadOnlyDictionary<string, long?> TenantQuotaBytes { get; init; }
        = new Dictionary<string, long?>();

    /// <summary>
    /// Per-tenant override for the MIME whitelist. When a tenant has no
    /// entry, the policy uses <see cref="Services.DefaultMimeWhitelist.Defaults"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> MimeWhitelistPerTenant { get; init; }
        = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>
    /// Inline-tier threshold. Bytes at or below this size go to the
    /// <see cref="StorageRefKind.Inline"/> tier (catalog row); larger
    /// payloads route to <see cref="StorageRefKind.FoundationBlob"/>.
    /// Default 8 KB.
    /// </summary>
    public int InlineBlobMaxBytes { get; init; } = 8 * 1024;

    /// <summary>Effective per-attachment cap for <paramref name="tenantId"/>.</summary>
    public long GetMaxAttachmentBytes(string tenantId) =>
        MaxAttachmentBytesPerTenant.TryGetValue(tenantId, out var cap) ? cap : MaxAttachmentBytes;

    /// <summary>Effective cumulative quota for <paramref name="tenantId"/>; null = unlimited.</summary>
    public long? GetTenantQuotaBytes(string tenantId) =>
        TenantQuotaBytes.TryGetValue(tenantId, out var q) ? q : null;

    /// <summary>Effective MIME whitelist for <paramref name="tenantId"/>.</summary>
    public IReadOnlySet<string> GetAllowedMimeTypes(string tenantId) =>
        MimeWhitelistPerTenant.TryGetValue(tenantId, out var custom)
            ? custom
            : Services.DefaultMimeWhitelist.Defaults;
}

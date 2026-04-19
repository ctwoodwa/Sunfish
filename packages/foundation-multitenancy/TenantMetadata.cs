using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Descriptive record for a single tenant known to the host. Identity-only
/// shape — no permissions or user context.
/// </summary>
public sealed record TenantMetadata
{
    /// <summary>Stable tenant identifier.</summary>
    public required TenantId Id { get; init; }

    /// <summary>Short, routable tenant name (e.g. subdomain slug).</summary>
    public required string Name { get; init; }

    /// <summary>Tenant lifecycle status.</summary>
    public TenantStatus Status { get; init; } = TenantStatus.Active;

    /// <summary>Optional human-friendly display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Optional BCP-47 locale tag.</summary>
    public string? Locale { get; init; }

    /// <summary>Optional creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Free-form extension properties (host-specific metadata).</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; }
        = new Dictionary<string, string>();
}

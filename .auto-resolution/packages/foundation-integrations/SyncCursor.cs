using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Last-synced position for a <c>(provider, tenant, scope)</c> tuple.
/// Cursor value is opaque bytes — each adapter defines its own semantics
/// (timestamp, sequence number, provider token).
/// </summary>
public sealed record SyncCursor
{
    /// <summary>Provider that owns the cursor.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Tenant scope; null for system-level cursors.</summary>
    public TenantId? TenantId { get; init; }

    /// <summary>Provider-defined scope (entity kind, stream name, …).</summary>
    public required string Scope { get; init; }

    /// <summary>Opaque cursor bytes.</summary>
    public required byte[] Value { get; init; }

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

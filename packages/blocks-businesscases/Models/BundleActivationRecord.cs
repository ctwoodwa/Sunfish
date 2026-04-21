using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.BusinessCases.Models;

/// <summary>
/// Persisted record of a tenant's current bundle activation. One record per
/// (tenant, bundle) pair; a tenant may hold multiple active bundles.
/// Tenant-scoped: every activation belongs to exactly one tenant.
/// </summary>
public sealed record BundleActivationRecord : IMustHaveTenant
{
    /// <summary>Unique identifier for this activation record.</summary>
    public required BundleActivationRecordId Id { get; init; }

    /// <summary>The tenant this activation applies to.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Bundle key from <c>IBundleCatalog</c> (reverse-DNS style).</summary>
    public required string BundleKey { get; init; }

    /// <summary>The edition selected from the bundle's <c>EditionMappings</c>.</summary>
    public required string Edition { get; init; }

    /// <summary>UTC timestamp the bundle was activated for this tenant.</summary>
    public required DateTimeOffset ActivatedAt { get; init; }

    /// <summary>UTC timestamp the bundle was deactivated for this tenant, or null while active.</summary>
    public DateTimeOffset? DeactivatedAt { get; init; }
}

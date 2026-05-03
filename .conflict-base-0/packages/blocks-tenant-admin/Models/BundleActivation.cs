using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// Records that a tenant has activated a specific bundle at a specific edition.
/// <see cref="BundleKey"/> matches <c>BusinessCaseBundleManifest.Key</c> and
/// <see cref="Edition"/> matches a key from that manifest's <c>EditionMappings</c>.
/// A deactivated activation has <see cref="DeactivatedAt"/> set; the row is not
/// hard-deleted so audit history is retained.
/// </summary>
public sealed record BundleActivation : IMustHaveTenant
{
    /// <summary>Unique identifier for this activation record.</summary>
    public required BundleActivationId Id { get; init; }

    /// <summary>The tenant that owns this activation.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The bundle key (matches <c>BusinessCaseBundleManifest.Key</c>).</summary>
    public required string BundleKey { get; init; }

    /// <summary>The edition key (matches a key from <c>BusinessCaseBundleManifest.EditionMappings</c>).</summary>
    public required string Edition { get; init; }

    /// <summary>UTC timestamp when activation occurred.</summary>
    public required DateTime ActivatedAt { get; init; }

    /// <summary>UTC timestamp when the activation was deactivated, or null if still active.</summary>
    public DateTime? DeactivatedAt { get; init; }
}

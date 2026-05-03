using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// A point-in-time usage sample recorded against a <see cref="UsageMeter"/>.
/// Tenant-scoped: samples always inherit the tenant of the owning meter.
/// </summary>
public sealed record MeteredUsage : IMustHaveTenant
{
    /// <summary>Unique identifier for this usage record.</summary>
    public required Guid Id { get; init; }

    /// <summary>The tenant that owns this usage record.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The meter this usage sample was recorded against.</summary>
    public required UsageMeterId MeterId { get; init; }

    /// <summary>Quantity consumed in this sample (non-negative).</summary>
    public required decimal Quantity { get; init; }

    /// <summary>UTC timestamp at which the usage was observed.</summary>
    public required DateTime RecordedAtUtc { get; init; }
}

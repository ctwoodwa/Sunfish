using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// A named metered dimension (e.g. "api-calls", "seats-used") attached to a
/// tenant's <see cref="Subscription"/>. The running quantity is aggregated
/// from <see cref="MeteredUsage"/> records written against this meter.
/// </summary>
public sealed record UsageMeter : IMustHaveTenant
{
    /// <summary>Unique identifier for this usage meter.</summary>
    public required UsageMeterId Id { get; init; }

    /// <summary>The tenant that owns this meter.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The subscription this meter is attached to.</summary>
    public required SubscriptionId SubscriptionId { get; init; }

    /// <summary>Stable meter code (e.g. <c>"api-calls"</c>, <c>"seats-used"</c>).</summary>
    public required string Code { get; init; }

    /// <summary>Unit label (e.g. <c>"calls"</c>, <c>"seats"</c>).</summary>
    public required string Unit { get; init; }
}

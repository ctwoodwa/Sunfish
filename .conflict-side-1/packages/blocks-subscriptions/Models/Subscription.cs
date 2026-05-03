using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// A tenant's active subscription to a <see cref="Plan"/>.
/// Tenant-scoped: every subscription belongs to exactly one tenant.
/// </summary>
public sealed record Subscription : IMustHaveTenant
{
    /// <summary>Unique identifier for this subscription.</summary>
    public required SubscriptionId Id { get; init; }

    /// <summary>The tenant that owns this subscription.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The plan this subscription is tied to.</summary>
    public required PlanId PlanId { get; init; }

    /// <summary>The pricing/feature tier for this subscription.</summary>
    public required Edition Edition { get; init; }

    /// <summary>UTC date the subscription started (inclusive).</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>
    /// UTC date the subscription ends (inclusive), or <see langword="null"/>
    /// for an open-ended subscription.
    /// </summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Identifiers of add-ons currently attached to this subscription.</summary>
    public IReadOnlyList<AddOnId> AddOns { get; init; } = [];
}

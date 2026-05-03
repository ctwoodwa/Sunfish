using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.Services;

/// <summary>
/// Payload for creating a new <see cref="Subscription"/> via
/// <see cref="ISubscriptionService.CreateSubscriptionAsync"/>.
/// </summary>
public sealed record CreateSubscriptionRequest
{
    /// <summary>The plan to subscribe to.</summary>
    public required PlanId PlanId { get; init; }

    /// <summary>The pricing/feature tier for this subscription.</summary>
    public required Edition Edition { get; init; }

    /// <summary>UTC date the subscription starts (inclusive).</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>
    /// UTC date the subscription ends (inclusive), or <see langword="null"/>
    /// for an open-ended subscription.
    /// </summary>
    public DateOnly? EndDate { get; init; }
}

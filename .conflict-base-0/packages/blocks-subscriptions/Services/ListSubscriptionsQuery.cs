using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.Services;

/// <summary>
/// Optional filter parameters for <see cref="ISubscriptionService.ListSubscriptionsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListSubscriptionsQuery
{
    /// <summary>When set, only subscriptions on this plan are returned.</summary>
    public PlanId? PlanId { get; init; }

    /// <summary>When set, only subscriptions on this edition are returned.</summary>
    public Edition? Edition { get; init; }

    /// <summary>
    /// Shared empty query that applies no filters.
    /// </summary>
    public static ListSubscriptionsQuery Empty { get; } = new();
}

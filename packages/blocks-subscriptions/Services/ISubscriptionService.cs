using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.Services;

/// <summary>
/// Contract for managing subscription records and related catalog lookups.
/// Implementations may be in-memory (for testing/demo) or persistence-backed (production).
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Streams the full catalog of available <see cref="Plan"/>s.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Plan> ListPlansAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the subscription with the specified <paramref name="id"/>, or
    /// <see langword="null"/> if no such subscription exists (or if it does
    /// not belong to the current tenant).
    /// </summary>
    /// <param name="id">The subscription identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Subscription?> GetSubscriptionAsync(SubscriptionId id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new subscription for the current tenant from <paramref name="request"/>
    /// and returns the persisted record.
    /// </summary>
    /// <param name="request">The creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Subscription> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams subscriptions for the current tenant that match <paramref name="query"/>.
    /// Pass <see cref="ListSubscriptionsQuery.Empty"/> to return all subscriptions
    /// for the current tenant.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Subscription> ListSubscriptionsAsync(ListSubscriptionsQuery query, CancellationToken ct = default);

    /// <summary>
    /// Attaches the specified add-on to the subscription and returns the updated
    /// subscription. Duplicate adds are idempotent.
    /// </summary>
    /// <param name="subscriptionId">The subscription to modify.</param>
    /// <param name="addOnId">The add-on to attach.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Subscription> AddAddOnAsync(SubscriptionId subscriptionId, AddOnId addOnId, CancellationToken ct = default);

    /// <summary>
    /// Records a usage sample against the specified meter.
    /// </summary>
    /// <param name="meterId">The meter to record usage against.</param>
    /// <param name="quantity">The quantity consumed (non-negative).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted <see cref="MeteredUsage"/> record.</returns>
    ValueTask<MeteredUsage> RecordUsageAsync(UsageMeterId meterId, decimal quantity, CancellationToken ct = default);
}

using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.State;

/// <summary>
/// UI state for the <c>SubscriptionListBlock</c> component: the current list of
/// subscriptions, a loading flag, and an optional error message.
/// </summary>
/// <param name="Subscriptions">Subscriptions currently displayed by the list.</param>
/// <param name="IsLoading">
/// <see langword="true"/> while the list is loading; <see langword="false"/>
/// once the initial load has settled (success or error).
/// </param>
/// <param name="Error">
/// An error message to surface to the user, or <see langword="null"/> if the
/// most recent load succeeded.
/// </param>
public sealed record SubscriptionListState(
    IReadOnlyList<Subscription> Subscriptions,
    bool IsLoading,
    string? Error)
{
    /// <summary>Initial state: empty list, loading, no error.</summary>
    public static SubscriptionListState Loading { get; } =
        new([], IsLoading: true, Error: null);

    /// <summary>Shared empty state: no subscriptions, not loading, no error.</summary>
    public static SubscriptionListState Empty { get; } =
        new([], IsLoading: false, Error: null);
}

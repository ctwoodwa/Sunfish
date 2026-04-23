using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// Per-team notification source consumed by <see cref="INotificationAggregator"/>.
/// One implementation is registered per team the local node is sync'ing.
/// </summary>
/// <remarks>
/// This is the seam Wave 6.3 replaces. The scaffold uses
/// <see cref="EmptyTeamNotificationStream"/> so downstream work (UI team
/// switcher, badge binding) can target a stable contract. Wave 6.3 swaps in
/// a real gossip- / event-log-backed implementation per team.
/// </remarks>
public interface ITeamNotificationStream
{
    /// <summary>Team this stream is scoped to.</summary>
    TeamId TeamId { get; }

    /// <summary>
    /// Subscribes to the team's notification stream. Runs until
    /// <paramref name="ct"/> is cancelled; enumerator must complete cleanly
    /// on cancellation (no exception leaks to the aggregator pump).
    /// </summary>
    /// <param name="ct">Cancellation token used by the aggregator's pump task.</param>
    IAsyncEnumerable<TeamNotification> Subscribe(CancellationToken ct);
}

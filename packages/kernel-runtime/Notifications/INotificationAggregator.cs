using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// Fan-in of every team's <see cref="ITeamNotificationStream"/> plus unread
/// bookkeeping. Implements ADR 0032's "all teams sync in background, one team
/// renders in foreground" model: per-team badges come from
/// <see cref="GetUnreadCount"/>, the tray / window-title aggregate from
/// <see cref="GetAggregateUnreadCount"/>, and the live UI feed from
/// <see cref="SubscribeAll"/>.
/// </summary>
/// <remarks>
/// Sits above <c>IActiveTeamAccessor</c>: the aggregator does not care which
/// team is active — it surfaces traffic from every team the user belongs to.
/// The active team only drives which subset the UI chooses to render inline.
/// </remarks>
public interface INotificationAggregator
{
    /// <summary>Unread count for a single team (per-team badge).</summary>
    /// <param name="teamId">Team to query. Unknown teams return <c>0</c>.</param>
    int GetUnreadCount(TeamId teamId);

    /// <summary>Sum of unread counts across every team — the cross-team total
    /// used for the system tray / window title / tab-title aggregate.</summary>
    int GetAggregateUnreadCount();

    /// <summary>Fan-in of every per-team stream. Items are interleaved in the
    /// order the aggregator received them from the underlying streams.</summary>
    /// <param name="ct">Cancellation token used by the consumer.</param>
    IAsyncEnumerable<TeamNotification> SubscribeAll(CancellationToken ct);

    /// <summary>Convenience passthrough — subscribe to a single team's stream
    /// via the aggregator rather than resolving the stream directly.</summary>
    /// <param name="teamId">Team whose stream to subscribe to. Unknown teams
    /// produce an empty sequence.</param>
    /// <param name="ct">Cancellation token used by the consumer.</param>
    IAsyncEnumerable<TeamNotification> SubscribeTeam(TeamId teamId, CancellationToken ct);

    /// <summary>
    /// Marks a notification as read, decrementing
    /// <see cref="GetUnreadCount"/> and <see cref="GetAggregateUnreadCount"/>.
    /// No-ops for unknown team or notification id.
    /// </summary>
    /// <param name="teamId">Team the notification belongs to.</param>
    /// <param name="notificationId">The <see cref="TeamNotification.Id"/> to mark read.</param>
    /// <param name="ct">Cancellation token. Currently honoured only for the
    /// outer <see cref="ValueTask"/> — the in-memory scaffold completes synchronously.</param>
    ValueTask MarkReadAsync(TeamId teamId, string notificationId, CancellationToken ct);

    /// <summary>
    /// Raised synchronously on the pump thread whenever a notification arrives
    /// from any team. Handlers are expected to marshal to the UI thread
    /// themselves; the aggregator does no dispatch.
    /// </summary>
    event EventHandler<TeamNotification>? NotificationReceived;
}

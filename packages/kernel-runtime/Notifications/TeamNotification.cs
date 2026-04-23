using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// A single notification sourced from a team's gossip / event-log stream.
/// Part of ADR 0032's "all teams sync in background, one team renders in
/// foreground" model: notifications arrive for every team the user belongs
/// to, so UI can show per-team badges plus a cross-team aggregate count.
/// </summary>
/// <param name="TeamId">Team that produced the notification.</param>
/// <param name="Id">Stable per-team identifier used for de-dup and
/// <see cref="INotificationAggregator.MarkReadAsync"/>.</param>
/// <param name="Title">Short headline, suitable for a tray tooltip.</param>
/// <param name="Summary">Body text, suitable for an expanded notification panel.</param>
/// <param name="OccurredAt">UTC timestamp the underlying event was observed.</param>
/// <param name="Severity">Severity hint for UI styling / grouping.</param>
public readonly record struct TeamNotification(
    TeamId TeamId,
    string Id,
    string Title,
    string Summary,
    DateTimeOffset OccurredAt,
    NotificationSeverity Severity);

/// <summary>
/// Severity bucket for a <see cref="TeamNotification"/>. Per ADR 0032's
/// all-teams-sync / one-renders model — the aggregator treats every severity
/// uniformly for unread-count purposes; UI chooses styling.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Informational. No user action required.</summary>
    Info = 0,

    /// <summary>Something the user probably wants to know about soon.</summary>
    Warning = 1,

    /// <summary>A failure the user likely needs to act on.</summary>
    Error = 2,

    /// <summary>Explicit call for attention (mention, assignment, etc.).</summary>
    Attention = 3,
}

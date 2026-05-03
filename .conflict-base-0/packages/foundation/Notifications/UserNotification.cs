using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Notifications;

/// <summary>
/// Canonical user-notification record used across Sunfish accelerators.
///
/// This is the single source of truth for notifications surfaced to a user from any
/// domain source (task assignment, due-date change, comment, risk update, budget,
/// milestone, mention, file upload, …). It is persistence- and feed-friendly and
/// deliberately contains <b>no</b> UI/toast presentation concerns such as auto-close
/// duration, closeability, or theme colour — those live on
/// <see cref="Sunfish.Foundation.Models.NotificationModel"/>, which is a presentation channel
/// driven from this model via an explicit adapter.
/// </summary>
public sealed record UserNotification
{
    /// <summary>Stable identifier for this notification instance.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("n");

    /// <summary>The user this notification is addressed to. May be empty in single-user demo mode.</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Short headline. Optional — feed renders Message alone if absent.</summary>
    public string? Title { get; init; }

    /// <summary>Required human-readable message body.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>UTC creation timestamp; drives feed ordering.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Originating subsystem (e.g. "Tasks", "Risks", "Budget", "Comments", "Milestones", "Files").</summary>
    public NotificationSource Source { get; init; } = NotificationSource.System;

    /// <summary>Domain category for grouping/filtering in the inbox view.</summary>
    public NotificationCategory Category { get; init; } = NotificationCategory.Activity;

    /// <summary>Importance hint used by both feed sort/highlighting and toast severity mapping.</summary>
    public NotificationImportance Importance { get; init; } = NotificationImportance.Normal;

    /// <summary>Optional client-side route to navigate to when the user clicks the notification.</summary>
    public string? ActionUrl { get; init; }

    /// <summary>Whether the user has read this notification.</summary>
    public bool IsRead { get; init; }

    /// <summary>UTC timestamp of read transition. Null while unread.</summary>
    public DateTimeOffset? ReadAtUtc { get; init; }

    /// <summary>
    /// Optional dedupe key. Two notifications with the same non-empty key collapse:
    /// the existing record's timestamp/message is refreshed instead of inserting a duplicate
    /// (e.g. "task:1044:due-changed" — repeated due-date changes shouldn't spam the inbox).
    /// </summary>
    public string? CorrelationKey { get; init; }

    /// <summary>Delivery policy — whether this notification should also be projected to a toast.</summary>
    public NotificationDelivery Delivery { get; init; } = NotificationDelivery.FeedOnly;

    /// <summary>
    /// Optional bag for source-specific context (entity ids, assignee, etc.) used by feed
    /// renderers without forcing per-source schemas. Kept off the hot path.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

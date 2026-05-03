using System;

namespace Sunfish.Foundation.Notifications;

/// <summary>Origin subsystem of a <see cref="UserNotification"/>. Drives icon + grouping.</summary>
public enum NotificationSource
{
    System,
    Tasks,
    Risks,
    Budget,
    Comments,
    Milestones,
    Files,
    Mentions,
    Assistant,
}

/// <summary>Coarse user-facing category for inbox filtering.</summary>
public enum NotificationCategory
{
    Activity,
    Assignment,
    Mention,
    DueDate,
    Risk,
    Budget,
    Milestone,
    Comment,
    File,
    System,
}

/// <summary>Importance hint that the toast adapter maps to a <see cref="Sunfish.Foundation.Enums.ToastSeverity"/>.</summary>
public enum NotificationImportance
{
    Low,
    Normal,
    High,
    Critical,
}

/// <summary>
/// Where a canonical notification should appear. Flags so a single record can be both
/// persisted to the feed and surfaced as a transient toast in one create call.
/// </summary>
[Flags]
public enum NotificationDelivery
{
    FeedOnly = 1,
    ToastOnly = 2,
    FeedAndToast = FeedOnly | ToastOnly,
}

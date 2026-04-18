using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Notifications;

/// <summary>
/// Canonical notification pipeline. Owns creation, persistence, querying, read-state,
/// dedupe, and change notification for every notification the user sees in the bell,
/// the inbox, or as a toast.
///
/// <para>
/// Accelerators register a concrete implementation (in-memory, database-backed, etc.).
/// Toast presentation is reached through the registered <see cref="IUserNotificationToastForwarder"/>.
/// </para>
/// </summary>
public interface IUserNotificationService
{
    /// <summary>All notifications for the current user, newest first.</summary>
    IReadOnlyList<UserNotification> All { get; }

    /// <summary>Convenience accessor — number of unread notifications.</summary>
    int UnreadCount { get; }

    /// <summary>Raised whenever the underlying list mutates.</summary>
    event Action? Changed;

    /// <summary>
    /// Create a notification from any source. If <paramref name="notification"/> has a
    /// non-empty <see cref="UserNotification.CorrelationKey"/> and a record with the same
    /// key already exists, the existing record is refreshed (message/timestamp) instead
    /// of inserting a duplicate. Returns the persisted record (which may differ from the
    /// input if a dedupe occurred).
    /// </summary>
    Task<UserNotification> CreateAsync(UserNotification notification, CancellationToken ct = default);

    /// <summary>Mark a single notification as read. No-op if id is unknown or already read.</summary>
    Task MarkReadAsync(string id, CancellationToken ct = default);

    /// <summary>Mark every unread notification as read.</summary>
    Task MarkAllReadAsync(CancellationToken ct = default);

    /// <summary>Remove a single notification.</summary>
    Task DismissAsync(string id, CancellationToken ct = default);

    /// <summary>Remove all read notifications (used by the bell "Delete all read" action).</summary>
    Task DeleteAllReadAsync(CancellationToken ct = default);

    /// <summary>Replace the entire store, e.g. for seeding or test reset.</summary>
    Task ReplaceAllAsync(IEnumerable<UserNotification> items, CancellationToken ct = default);
}

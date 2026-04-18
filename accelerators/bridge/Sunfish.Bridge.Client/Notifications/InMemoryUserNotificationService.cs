using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Client.Notifications;

/// <summary>
/// In-memory implementation of <see cref="IUserNotificationService"/> for the PM Demo.
/// Structured so the storage layer can be swapped for a persistent backing store
/// (e.g. DAB-backed table) without touching consumers — only this class would change.
/// </summary>
public sealed class InMemoryUserNotificationService : IUserNotificationService
{
    private readonly object _gate = new();
    private readonly List<UserNotification> _items = new();
    private readonly IUserNotificationToastForwarder? _toastForwarder;

    public InMemoryUserNotificationService(IUserNotificationToastForwarder? toastForwarder = null)
    {
        _toastForwarder = toastForwarder;
        SeedDemoData();
    }

    /// <inheritdoc />
    public IReadOnlyList<UserNotification> All
    {
        get
        {
            lock (_gate)
            {
                return _items
                    .OrderByDescending(n => n.CreatedAtUtc)
                    .ToArray();
            }
        }
    }

    /// <inheritdoc />
    public int UnreadCount
    {
        get
        {
            lock (_gate)
            {
                return _items.Count(n => !n.IsRead);
            }
        }
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public Task<UserNotification> CreateAsync(UserNotification notification, CancellationToken ct = default)
    {
        UserNotification stored;

        lock (_gate)
        {
            if (!string.IsNullOrEmpty(notification.CorrelationKey))
            {
                var existing = _items.FirstOrDefault(n => n.CorrelationKey == notification.CorrelationKey);
                if (existing is not null)
                {
                    var refreshed = existing with
                    {
                        Message = notification.Message,
                        Title = notification.Title ?? existing.Title,
                        CreatedAtUtc = notification.CreatedAtUtc == default ? DateTimeOffset.UtcNow : notification.CreatedAtUtc,
                        Importance = notification.Importance,
                        ActionUrl = notification.ActionUrl ?? existing.ActionUrl,
                        IsRead = false,
                        ReadAtUtc = null,
                    };
                    var idx = _items.IndexOf(existing);
                    _items[idx] = refreshed;
                    stored = refreshed;
                }
                else
                {
                    stored = notification with
                    {
                        Id = string.IsNullOrEmpty(notification.Id) ? Guid.NewGuid().ToString("n") : notification.Id,
                        CreatedAtUtc = notification.CreatedAtUtc == default ? DateTimeOffset.UtcNow : notification.CreatedAtUtc,
                    };
                    _items.Add(stored);
                }
            }
            else
            {
                stored = notification with
                {
                    Id = string.IsNullOrEmpty(notification.Id) ? Guid.NewGuid().ToString("n") : notification.Id,
                    CreatedAtUtc = notification.CreatedAtUtc == default ? DateTimeOffset.UtcNow : notification.CreatedAtUtc,
                };
                _items.Add(stored);
            }
        }

        // Toast forwarding happens outside the lock so a slow forwarder can't block writers.
        if ((stored.Delivery & NotificationDelivery.ToastOnly) != 0)
        {
            _toastForwarder?.Forward(stored);
        }

        Changed?.Invoke();
        return Task.FromResult(stored);
    }

    /// <inheritdoc />
    public Task MarkReadAsync(string id, CancellationToken ct = default)
    {
        var changed = false;
        lock (_gate)
        {
            var idx = _items.FindIndex(n => n.Id == id);
            if (idx >= 0 && !_items[idx].IsRead)
            {
                _items[idx] = _items[idx] with { IsRead = true, ReadAtUtc = DateTimeOffset.UtcNow };
                changed = true;
            }
        }
        if (changed) Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var changed = false;
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (!_items[i].IsRead)
                {
                    _items[i] = _items[i] with { IsRead = true, ReadAtUtc = now };
                    changed = true;
                }
            }
        }
        if (changed) Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DismissAsync(string id, CancellationToken ct = default)
    {
        var changed = false;
        lock (_gate)
        {
            changed = _items.RemoveAll(n => n.Id == id) > 0;
        }
        if (changed) Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAllReadAsync(CancellationToken ct = default)
    {
        var changed = false;
        lock (_gate)
        {
            changed = _items.RemoveAll(n => n.IsRead) > 0;
        }
        if (changed) Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReplaceAllAsync(IEnumerable<UserNotification> items, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _items.Clear();
            _items.AddRange(items);
        }
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private void SeedDemoData()
    {
        var now = DateTimeOffset.UtcNow;
        _items.AddRange(new[]
        {
            new UserNotification
            {
                Title = "Sprint 14",
                Message = "Planning completed — 18 stories committed.",
                CreatedAtUtc = now.AddMinutes(-12),
                Source = NotificationSource.Milestones,
                Category = NotificationCategory.Milestone,
                Importance = NotificationImportance.Normal,
                ActionUrl = "/timeline",
            },
            new UserNotification
            {
                Title = "Risk R-203",
                Message = "Severity escalated to High by Priya Patel.",
                CreatedAtUtc = now.AddHours(-2),
                Source = NotificationSource.Risks,
                Category = NotificationCategory.Risk,
                Importance = NotificationImportance.High,
                ActionUrl = "/risk",
                CorrelationKey = "risk:R-203:severity",
            },
            new UserNotification
            {
                Title = "Budget — Phase 2",
                Message = "Spend crossed 80% of approved threshold.",
                CreatedAtUtc = now.AddHours(-26),
                Source = NotificationSource.Budget,
                Category = NotificationCategory.Budget,
                Importance = NotificationImportance.High,
                ActionUrl = "/budget",
                CorrelationKey = "budget:phase-2:threshold-80",
            },
            new UserNotification
            {
                Title = "Task T-1044 assigned",
                Message = "Avery Chen → \"Wire DAB user_preferences entity\".",
                CreatedAtUtc = now.AddHours(-3),
                Source = NotificationSource.Tasks,
                Category = NotificationCategory.Assignment,
                Importance = NotificationImportance.Normal,
                ActionUrl = "/tasks",
                CorrelationKey = "task:T-1044:assigned",
            },
            new UserNotification
            {
                Title = "Mention in Atlas standup",
                Message = "Jordan Lee mentioned you: \"@avery can you confirm the release window?\"",
                CreatedAtUtc = now.AddHours(-5),
                Source = NotificationSource.Mentions,
                Category = NotificationCategory.Mention,
                Importance = NotificationImportance.Normal,
                ActionUrl = "/board",
            },
            new UserNotification
            {
                Title = "Comment on T-987",
                Message = "Sam Rivera replied to your design note.",
                CreatedAtUtc = now.AddHours(-8),
                Source = NotificationSource.Comments,
                Category = NotificationCategory.Comment,
                Importance = NotificationImportance.Low,
                ActionUrl = "/tasks",
                IsRead = true,
                ReadAtUtc = now.AddHours(-7),
            },
            new UserNotification
            {
                Title = "File uploaded",
                Message = "RFC-014.pdf attached to T-1102 by Lin Park.",
                CreatedAtUtc = now.AddHours(-30),
                Source = NotificationSource.Files,
                Category = NotificationCategory.File,
                Importance = NotificationImportance.Low,
                ActionUrl = "/tasks",
                IsRead = true,
                ReadAtUtc = now.AddHours(-29),
            },
            new UserNotification
            {
                Title = "Due date changed",
                Message = "T-902 \"Migration smoke test\" moved from Apr 10 → Apr 14.",
                CreatedAtUtc = now.AddHours(-50),
                Source = NotificationSource.Tasks,
                Category = NotificationCategory.DueDate,
                Importance = NotificationImportance.Normal,
                ActionUrl = "/tasks",
                CorrelationKey = "task:T-902:due-changed",
            },
        });
    }
}

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// In-memory <see cref="IIdempotencyCache"/> backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> with
/// time-based eviction (ADR 0031-A1.5: 24-hour retention default).
/// Entries are evicted on next access past the retention window.
/// </summary>
public sealed class InMemoryIdempotencyCache : IIdempotencyCache
{
    /// <summary>Default per A1.5.</summary>
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<(string TenantId, Guid EventId), DateTimeOffset> _seen = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _retention;

    public InMemoryIdempotencyCache(TimeProvider? time = null, TimeSpan? retention = null)
    {
        _time = time ?? TimeProvider.System;
        _retention = retention ?? DefaultRetention;
    }

    /// <inheritdoc />
    public ValueTask<bool> TryClaimAsync(string tenantId, Guid eventId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ct.ThrowIfCancellationRequested();
        var key = (tenantId, eventId);
        var now = _time.GetUtcNow();

        if (_seen.TryGetValue(key, out var seenAt))
        {
            if (now - seenAt < _retention)
            {
                return ValueTask.FromResult(true); // already claimed within window
            }
            // Stale entry — evict + re-record.
            _seen.TryRemove(key, out _);
        }
        _seen[key] = now;
        return ValueTask.FromResult(false);
    }
}

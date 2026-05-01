using System;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Per ADR 0031-A1.12.4 — when a Tier-2 SSE connection drops, Bridge
/// queues events per-tenant for up to <see cref="DefaultMaxAge"/> OR
/// <see cref="DefaultMaxEvents"/>. If either threshold is crossed, the
/// queue overflows and Bridge falls back to webhook delivery for the
/// pending events (the disconnect window has gone too long for SSE
/// recovery to be useful).
/// </summary>
public sealed class SseQueueOverflowPolicy
{
    /// <summary>1-hour max queue age per A1.12.4.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(1);

    /// <summary>10,000-event max queue depth per A1.12.4.</summary>
    public const int DefaultMaxEvents = 10_000;

    private readonly TimeSpan _maxAge;
    private readonly int _maxEvents;

    public SseQueueOverflowPolicy(TimeSpan? maxAge = null, int? maxEvents = null)
    {
        _maxAge = maxAge ?? DefaultMaxAge;
        _maxEvents = maxEvents ?? DefaultMaxEvents;
    }

    /// <summary>
    /// True when the queue should overflow + the events should be
    /// delivered via webhook fallback. Either threshold (age OR depth)
    /// triggers overflow.
    /// </summary>
    public bool HasOverflowed(int queueDepth, TimeSpan oldestEntryAge)
    {
        if (queueDepth >= _maxEvents) return true;
        if (oldestEntryAge >= _maxAge) return true;
        return false;
    }
}

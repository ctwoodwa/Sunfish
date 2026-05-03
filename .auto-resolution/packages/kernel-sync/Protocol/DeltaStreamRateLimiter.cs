using System.Collections.Concurrent;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Per-peer token-bucket rate limiter for DELTA_STREAM traffic.
/// Sync-daemon-protocol §8 "Rate limiting": default 1000 messages/sec/peer.
/// </summary>
/// <remarks>
/// <para>
/// A bucket holds up to <c>capacity</c> tokens and refills at a steady rate
/// of <c>capacity</c> tokens per second (i.e. <c>capacity</c> tokens / 1000 ms).
/// Each <see cref="TryConsume"/> call takes one token; above-limit calls
/// return <c>false</c> and the caller drops the message with a warning
/// log line.
/// </para>
/// <para>
/// <b>Implementation choice.</b> The bucket is a lazy-refill counter —
/// rather than a background timer, each <see cref="TryConsume"/> computes
/// the tokens that should have accrued since the last call and tops up
/// the bucket (capped at <c>capacity</c>). This gives correct per-second
/// behaviour without any timer threads and without allocating on the hot
/// path beyond the initial <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// lookup. Stopwatch ticks are used for the clock because they are
/// monotonic (wall-clock adjustments do not perturb the refill cadence).
/// </para>
/// <para>
/// <b>Thread-safety.</b> The per-peer bucket serializes its update under a
/// short lock so concurrent receive loops from the same peer cannot
/// race. The outer dictionary is lock-free for lookup; <see cref="TryConsume"/>
/// allocates at most one new bucket per previously-unseen peer.
/// </para>
/// </remarks>
public sealed class DeltaStreamRateLimiter
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly int _capacity;

    /// <summary>
    /// Create a limiter with the given per-peer capacity (tokens/sec). The
    /// default 1000 matches sync-daemon-protocol §8.
    /// </summary>
    public DeltaStreamRateLimiter(int capacityPerPeerPerSecond = 1000)
    {
        if (capacityPerPeerPerSecond < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityPerPeerPerSecond),
                "Capacity must be at least 1.");
        }
        _capacity = capacityPerPeerPerSecond;
    }

    /// <summary>
    /// Configured token-bucket capacity (tokens per peer per second).
    /// </summary>
    public int CapacityPerPeerPerSecond => _capacity;

    /// <summary>
    /// Attempt to consume one token from <paramref name="peerEndpoint"/>'s
    /// bucket. Returns <c>true</c> if a token was available and was
    /// consumed; <c>false</c> if the peer exceeded its budget and the
    /// caller should drop the inbound DELTA_STREAM.
    /// </summary>
    public bool TryConsume(string peerEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerEndpoint);
        var bucket = _buckets.GetOrAdd(peerEndpoint, _ => new Bucket(_capacity));
        return bucket.TryConsume();
    }

    /// <summary>
    /// Reset the bucket for <paramref name="peerEndpoint"/> (tokens refilled
    /// to capacity, timestamp reset to now). Typically called when a peer
    /// reconnects; tests use it to cleanly re-arm between cases.
    /// </summary>
    public void Reset(string peerEndpoint)
    {
        if (_buckets.TryGetValue(peerEndpoint, out var bucket))
        {
            bucket.Reset();
        }
    }

    /// <summary>
    /// One peer's bucket. Lazy-refill counter keyed by
    /// <see cref="System.Diagnostics.Stopwatch"/> ticks for monotonicity.
    /// </summary>
    private sealed class Bucket
    {
        private readonly object _lock = new();
        private readonly int _capacity;

        // Tokens stored as double to represent fractional refills without
        // drift between TryConsume calls separated by sub-second intervals.
        private double _tokens;
        private long _lastRefillTicks;

        public Bucket(int capacity)
        {
            _capacity = capacity;
            _tokens = capacity;
            _lastRefillTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
                return false;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _tokens = _capacity;
                _lastRefillTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }

        private void Refill()
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsedSeconds =
                (double)(now - _lastRefillTicks) / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedSeconds <= 0)
            {
                return;
            }
            var add = elapsedSeconds * _capacity;
            _tokens = Math.Min(_capacity, _tokens + add);
            _lastRefillTicks = now;
        }
    }
}

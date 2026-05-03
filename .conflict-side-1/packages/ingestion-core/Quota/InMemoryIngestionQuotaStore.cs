using System.Collections.Concurrent;

namespace Sunfish.Ingestion.Core.Quota;

/// <summary>
/// Single-process, in-memory implementation of <see cref="IIngestionQuotaStore"/> using a
/// <em>token-bucket</em> algorithm.
/// </summary>
/// <remarks>
/// <para>
/// Each tenant gets an independent <see cref="TokenBucket"/> that is created on first access and
/// stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/>. Bucket state mutations are
/// serialized through a per-bucket <c>lock</c> — cheap because contention is per-tenant, not
/// global.
/// </para>
/// <para>
/// Refill is computed <em>lazily</em>: on each access the code checks the elapsed time since the
/// last refill and credits the appropriate number of token intervals before performing the
/// requested operation. No background thread or timer is involved.
/// </para>
/// <para>
/// A <see cref="TimeProvider"/> is injected so that tests can control the clock. Pass
/// <see cref="TimeProvider.System"/> (or omit the parameter) for production use.
/// </para>
/// <para>
/// For distributed/multi-process scenarios, replace this with a Redis-backed implementation that
/// uses Lua scripts for atomic compare-and-swap. See GitHub issue
/// <c>sunfish/platform#TODO</c> for tracking.
/// </para>
/// </remarks>
public sealed class InMemoryIngestionQuotaStore : IIngestionQuotaStore
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly QuotaPolicy _defaultPolicy;
    private readonly Func<string, QuotaPolicy>? _policyResolver;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initialises a new store that applies <paramref name="defaultPolicy"/> to every tenant.
    /// </summary>
    /// <param name="defaultPolicy">Policy applied when no per-tenant override exists.</param>
    /// <param name="timeProvider">
    /// Clock source used for refill calculations. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    public InMemoryIngestionQuotaStore(QuotaPolicy defaultPolicy, TimeProvider? timeProvider = null)
    {
        defaultPolicy.Validate();
        _defaultPolicy = defaultPolicy;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Initialises a new store with a per-tenant policy resolver.
    /// </summary>
    /// <param name="defaultPolicy">
    /// Fallback policy when <paramref name="policyResolver"/> returns <c>null</c>.
    /// </param>
    /// <param name="policyResolver">
    /// Returns an override <see cref="QuotaPolicy"/> for a given tenant id, or <c>null</c> to
    /// use <paramref name="defaultPolicy"/>.
    /// </param>
    /// <param name="timeProvider">
    /// Clock source used for refill calculations. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    public InMemoryIngestionQuotaStore(
        QuotaPolicy defaultPolicy,
        Func<string, QuotaPolicy?> policyResolver,
        TimeProvider? timeProvider = null)
    {
        defaultPolicy.Validate();
        _defaultPolicy = defaultPolicy;
        _policyResolver = tenantId => policyResolver(tenantId) ?? defaultPolicy;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<bool> TryConsumeAsync(string tenantId, int tokensRequested, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (tokensRequested <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensRequested), "Must be > 0.");

        var bucket = GetOrCreateBucket(tenantId);
        var result = bucket.TryConsume(tokensRequested, _timeProvider.GetUtcNow());
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<QuotaStatus> GetStatusAsync(string tenantId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var bucket = GetOrCreateBucket(tenantId);
        var status = bucket.GetStatus(tenantId, _timeProvider.GetUtcNow());
        return ValueTask.FromResult(status);
    }

    private TokenBucket GetOrCreateBucket(string tenantId)
    {
        if (_buckets.TryGetValue(tenantId, out var existing))
            return existing;

        var policy = _policyResolver?.Invoke(tenantId) ?? _defaultPolicy;
        var bucket = new TokenBucket(policy, _timeProvider.GetUtcNow());

        // GetOrAdd is safe: if another thread won the race, we discard ours.
        return _buckets.GetOrAdd(tenantId, bucket);
    }

    // -----------------------------------------------------------------------------------------
    // Inner type — not public surface

    internal sealed class TokenBucket
    {
        private readonly QuotaPolicy _policy;
        private int _tokens;
        private DateTimeOffset _lastRefillAt;

        internal TokenBucket(QuotaPolicy policy, DateTimeOffset createdAt)
        {
            _policy = policy;
            _tokens = policy.Capacity;    // starts full
            _lastRefillAt = createdAt;
        }

        /// <summary>
        /// Applies any pending refill intervals then attempts to consume
        /// <paramref name="requested"/> tokens. Thread-safe via <c>lock</c>.
        /// </summary>
        internal bool TryConsume(int requested, DateTimeOffset now)
        {
            lock (this)
            {
                ApplyRefill(now);

                if (_tokens < requested)
                    return false;

                _tokens -= requested;
                return true;
            }
        }

        /// <summary>
        /// Returns current bucket state (after applying pending refill). Thread-safe.
        /// </summary>
        internal QuotaStatus GetStatus(string tenantId, DateTimeOffset now)
        {
            lock (this)
            {
                ApplyRefill(now);

                DateTimeOffset? nextRefill = _tokens >= _policy.Capacity
                    ? null
                    : _lastRefillAt + _policy.RefillInterval;

                return new QuotaStatus(tenantId, _tokens, _policy.Capacity, nextRefill);
            }
        }

        // Must be called while holding the lock.
        private void ApplyRefill(DateTimeOffset now)
        {
            if (now <= _lastRefillAt)
                return;

            var elapsed = now - _lastRefillAt;
            var intervals = (long)(elapsed.TotalMilliseconds / _policy.RefillInterval.TotalMilliseconds);

            if (intervals <= 0)
                return;

            var tokensToAdd = checked((long)intervals * _policy.RefillTokens);
            _tokens = (int)Math.Min(_policy.Capacity, _tokens + tokensToAdd);
            _lastRefillAt += TimeSpan.FromMilliseconds(intervals * _policy.RefillInterval.TotalMilliseconds);
        }
    }
}

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="DeltaStreamRateLimiter"/> — the per-peer
/// token-bucket that guards DELTA_STREAM against misbehaving / bursty
/// peers (sync-daemon-protocol §8 "Rate limiting").
/// </summary>
public class DeltaStreamRateLimiterTests
{
    [Fact]
    public void Under_Limit_All_Consumes_Pass()
    {
        var limiter = new DeltaStreamRateLimiter(capacityPerPeerPerSecond: 100);
        for (var i = 0; i < 50; i++)
        {
            Assert.True(limiter.TryConsume("peer-a"),
                $"Iteration {i} should have succeeded under the 100-token budget.");
        }
    }

    [Fact]
    public void At_Exact_Capacity_Last_Passes_Next_Blocks()
    {
        // Bucket starts full at `capacity`. Consume exactly `capacity`
        // tokens back-to-back — the N-th consume must succeed, the
        // (N+1)-th must fail because no refill has had time to accrue.
        const int capacity = 10;
        var limiter = new DeltaStreamRateLimiter(capacityPerPeerPerSecond: capacity);

        for (var i = 0; i < capacity; i++)
        {
            Assert.True(limiter.TryConsume("peer-a"), $"Token {i} should pass.");
        }
        // Immediately after draining: any refill accrued during the tight
        // loop is sub-token — the next consume must still be blocked.
        Assert.False(limiter.TryConsume("peer-a"), "Post-drain consume must block.");
    }

    [Fact]
    public async Task Tokens_Refill_After_Time_Passes()
    {
        // Capacity 10/sec → refills at 10 tokens/sec → 5 tokens accrue in
        // 500 ms. We drain the bucket, wait 500 ms, then expect ~5
        // successes before the bucket drains again.
        const int capacity = 10;
        var limiter = new DeltaStreamRateLimiter(capacityPerPeerPerSecond: capacity);

        // Drain the bucket.
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(limiter.TryConsume("peer-a"));
        }
        Assert.False(limiter.TryConsume("peer-a"));

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // Count how many consumes pass; with ~5 tokens refilled, we expect
        // at least 3 (lower bound loosely guards against scheduler jitter
        // without being flaky) and at most capacity.
        var refilled = 0;
        for (var i = 0; i < capacity; i++)
        {
            if (limiter.TryConsume("peer-a")) refilled++;
            else break;
        }
        Assert.InRange(refilled, 3, capacity);
    }

    [Fact]
    public void Per_Peer_Isolation_Budgets_Do_Not_Mix()
    {
        const int capacity = 5;
        var limiter = new DeltaStreamRateLimiter(capacityPerPeerPerSecond: capacity);

        // Fully drain peer A.
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(limiter.TryConsume("peer-a"));
        }
        Assert.False(limiter.TryConsume("peer-a"),
            "peer-a bucket should be empty.");

        // peer-b is untouched → full budget still available.
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(limiter.TryConsume("peer-b"),
                $"peer-b iteration {i} must not be affected by peer-a drain.");
        }
        Assert.False(limiter.TryConsume("peer-b"));
    }

    [Fact]
    public void Reset_Refills_Peer_Bucket_To_Capacity()
    {
        const int capacity = 3;
        var limiter = new DeltaStreamRateLimiter(capacityPerPeerPerSecond: capacity);

        Assert.True(limiter.TryConsume("peer-a"));
        Assert.True(limiter.TryConsume("peer-a"));
        Assert.True(limiter.TryConsume("peer-a"));
        Assert.False(limiter.TryConsume("peer-a"));

        limiter.Reset("peer-a");

        for (var i = 0; i < capacity; i++)
        {
            Assert.True(limiter.TryConsume("peer-a"),
                $"Post-reset iteration {i} should succeed.");
        }
    }
}

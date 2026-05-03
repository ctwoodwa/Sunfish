using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class IdempotencyTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
    private static readonly Guid EventB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

    [Fact]
    public async Task TryClaimAsync_FirstCall_ReturnsFalse_AndRecords()
    {
        var cache = new InMemoryIdempotencyCache(new FakeTimeProvider(Now));
        Assert.False(await cache.TryClaimAsync(TenantA, EventA));
    }

    [Fact]
    public async Task TryClaimAsync_RepeatedCall_ReturnsTrueWithinRetention()
    {
        var time = new FakeTimeProvider(Now);
        var cache = new InMemoryIdempotencyCache(time);
        await cache.TryClaimAsync(TenantA, EventA);

        time.Advance(TimeSpan.FromHours(12)); // within 24h window
        Assert.True(await cache.TryClaimAsync(TenantA, EventA));
    }

    [Fact]
    public async Task TryClaimAsync_CallPastRetention_ReturnsFalse_AndReclaims()
    {
        var time = new FakeTimeProvider(Now);
        var cache = new InMemoryIdempotencyCache(time);
        await cache.TryClaimAsync(TenantA, EventA);

        time.Advance(TimeSpan.FromHours(25)); // past 24h window
        Assert.False(await cache.TryClaimAsync(TenantA, EventA));
    }

    [Fact]
    public async Task TryClaimAsync_DifferentTenants_AreIndependent()
    {
        var cache = new InMemoryIdempotencyCache(new FakeTimeProvider(Now));
        Assert.False(await cache.TryClaimAsync(TenantA, EventA));
        Assert.False(await cache.TryClaimAsync(TenantB, EventA));
        // Both tenants now have EventA recorded; second attempt for each
        // returns true.
        Assert.True(await cache.TryClaimAsync(TenantA, EventA));
        Assert.True(await cache.TryClaimAsync(TenantB, EventA));
    }

    [Fact]
    public async Task TryClaimAsync_DifferentEventIds_AreIndependent()
    {
        var cache = new InMemoryIdempotencyCache(new FakeTimeProvider(Now));
        Assert.False(await cache.TryClaimAsync(TenantA, EventA));
        Assert.False(await cache.TryClaimAsync(TenantA, EventB));
    }

    [Fact]
    public async Task TryClaimAsync_NullTenant_Throws()
    {
        var cache = new InMemoryIdempotencyCache(new FakeTimeProvider(Now));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.TryClaimAsync(string.Empty, EventA).AsTask());
    }

    [Fact]
    public async Task TryClaimAsync_HonorsCancellation()
    {
        var cache = new InMemoryIdempotencyCache(new FakeTimeProvider(Now));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cache.TryClaimAsync(TenantA, EventA, cts.Token).AsTask());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}

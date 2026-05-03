using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class DeadLetterQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static BridgeSubscriptionEvent NewEvent(string tenantId) => new()
    {
        TenantId = tenantId,
        EventType = BridgeSubscriptionEventType.SubscriptionTierUpgraded,
        EditionBefore = "anchor-self-host",
        EditionAfter = "bridge-pro",
        EffectiveAt = Now,
        EventId = Guid.NewGuid(),
        DeliveryAttempt = 8,
        Signature = "hmac-sha256:placeholder",
    };

    [Fact]
    public async Task EnqueueAsync_RecordsEntry_WithReasonAndTimestamp()
    {
        var time = new FakeTimeProvider(Now);
        var dlq = new InMemoryDeadLetterQueue(time);
        var evt = NewEvent("tenant-a");

        await dlq.EnqueueAsync(evt, "http-503");

        var entries = await dlq.GetByTenantAsync("tenant-a");
        var entry = Assert.Single(entries);
        Assert.Equal(evt, entry.Event);
        Assert.Equal("http-503", entry.Reason);
        Assert.Equal(Now, entry.DeadLetteredAt);
    }

    [Fact]
    public async Task GetByTenantAsync_FiltersByTenant()
    {
        var dlq = new InMemoryDeadLetterQueue();
        await dlq.EnqueueAsync(NewEvent("tenant-a"), "x");
        await dlq.EnqueueAsync(NewEvent("tenant-b"), "y");
        await dlq.EnqueueAsync(NewEvent("tenant-a"), "z");

        var aEntries = await dlq.GetByTenantAsync("tenant-a");
        var bEntries = await dlq.GetByTenantAsync("tenant-b");

        Assert.Equal(2, aEntries.Count);
        Assert.Single(bEntries);
    }

    [Fact]
    public async Task EnqueueAsync_NullEvent_Throws()
    {
        var dlq = new InMemoryDeadLetterQueue();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dlq.EnqueueAsync(null!, "x").AsTask());
    }

    [Fact]
    public async Task EnqueueAsync_EmptyReason_Throws()
    {
        var dlq = new InMemoryDeadLetterQueue();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            dlq.EnqueueAsync(NewEvent("t"), string.Empty).AsTask());
    }

    [Fact]
    public async Task EnqueueAsync_HonorsCancellation()
    {
        var dlq = new InMemoryDeadLetterQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            dlq.EnqueueAsync(NewEvent("t"), "r", cts.Token).AsTask());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}

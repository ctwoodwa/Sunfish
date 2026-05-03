using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.EventBus.Tests;

/// <summary>
/// Coverage for the in-memory checkpoint store backing
/// <see cref="IEventBus.GetCheckpointAsync"/> and
/// <see cref="IEventBus.AdvanceCheckpointAsync"/>.
/// </summary>
public class CheckpointTests
{
    [Fact]
    public async Task GetCheckpointAsync_UnknownSubscriber_ReturnsNull()
    {
        var bus = new InMemoryEventBus(new Ed25519Verifier());

        var cp = await bus.GetCheckpointAsync("unknown-subscriber");

        Assert.Null(cp);
    }

    [Fact]
    public async Task AdvanceCheckpointAsync_ThenGet_ReturnsAdvancedPosition()
    {
        var bus = new InMemoryEventBus(new Ed25519Verifier());
        var eventId = EventId.NewId();

        await bus.AdvanceCheckpointAsync("sub-A", eventId);
        var cp = await bus.GetCheckpointAsync("sub-A");

        Assert.NotNull(cp);
        Assert.Equal("sub-A", cp!.SubscriberId);
        Assert.Equal(eventId, cp.LastProcessed);
        // AdvancedAt should be within the last few seconds.
        Assert.InRange(cp.AdvancedAt, DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task AdvanceCheckpointAsync_NewerAdvancesLaterReturned()
    {
        // Two back-to-back advances for the same subscriber; Get returns the latest.
        var bus = new InMemoryEventBus(new Ed25519Verifier());
        var first = EventId.NewId();
        var second = EventId.NewId();

        await bus.AdvanceCheckpointAsync("sub-B", first);
        await bus.AdvanceCheckpointAsync("sub-B", second);

        var cp = await bus.GetCheckpointAsync("sub-B");

        Assert.NotNull(cp);
        Assert.Equal(second, cp!.LastProcessed);
        Assert.NotEqual(first, cp.LastProcessed);
    }
}

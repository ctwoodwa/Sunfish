using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="InProcessEventDispatcher"/>.
/// </summary>
public sealed class InProcessEventDispatcherTests
{
    [Fact]
    public async Task Subscribe_AddsHandlerToFanout()
    {
        var sut = new InProcessEventDispatcher();
        var received = new List<RawDomainEvent>();
        sut.Subscribe((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        await sut.DispatchAsync(NewRaw("hit"));

        Assert.Single(received);
    }

    [Fact]
    public async Task DispatchAsync_InvokesAllSubscribers()
    {
        var sut = new InProcessEventDispatcher();
        int aCount = 0, bCount = 0, cCount = 0;
        sut.Subscribe((_, _) => { Interlocked.Increment(ref aCount); return Task.CompletedTask; });
        sut.Subscribe((_, _) => { Interlocked.Increment(ref bCount); return Task.CompletedTask; });
        sut.Subscribe((_, _) => { Interlocked.Increment(ref cCount); return Task.CompletedTask; });

        await sut.DispatchAsync(NewRaw("hit"));

        Assert.Equal(1, aCount);
        Assert.Equal(1, bCount);
        Assert.Equal(1, cCount);
    }

    [Fact]
    public async Task DispatchAsync_OnSubscriberThrow_OtherSubscribersStillInvoked()
    {
        var sut = new InProcessEventDispatcher();
        var goodHandlerCalled = false;
        sut.Subscribe((_, _) => throw new InvalidOperationException("bad handler"));
        sut.Subscribe((_, _) => { goodHandlerCalled = true; return Task.CompletedTask; });

        // Should NOT throw (failures isolated).
        await sut.DispatchAsync(NewRaw("hit"));

        Assert.True(goodHandlerCalled);
    }

    [Fact]
    public async Task DispatchAsync_OnNullEvent_Throws()
    {
        var sut = new InProcessEventDispatcher();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.DispatchAsync(null!));
    }

    [Fact]
    public void Subscribe_OnNullHandler_Throws()
    {
        var sut = new InProcessEventDispatcher();
        Assert.Throws<ArgumentNullException>(() => sut.Subscribe(null!));
    }

    [Fact]
    public async Task DispatchAsync_CompletesEvenIfNoSubscribers()
    {
        var sut = new InProcessEventDispatcher();
        await sut.DispatchAsync(NewRaw("none"));
        // Reaching this line is the assertion — no subscribers + no throw.
    }

    [Fact]
    public async Task DispatchAsync_CancellationTokenPropagatesToSubscriber()
    {
        var sut = new InProcessEventDispatcher();
        var cts = new CancellationTokenSource();
        Exception? captured = null;
        sut.Subscribe(async (_, ct) =>
        {
            try { await Task.Delay(5_000, ct); }
            catch (OperationCanceledException ex) { captured = ex; throw; }
        });

        var dispatchTask = sut.DispatchAsync(NewRaw("cancel"), cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatchTask);
        Assert.NotNull(captured);
    }

    private static RawDomainEvent NewRaw(string idempotencyKey)
        => new()
        {
            EventId              = EventId.New(),
            EventType            = "Test.X",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            RecordedAtUtc        = DateTimeOffset.UtcNow,
            TenantId             = new TenantId("dispatcher-test"),
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = idempotencyKey,
            ProducerCluster      = "test",
            PayloadJson          = "{}",
        };
}

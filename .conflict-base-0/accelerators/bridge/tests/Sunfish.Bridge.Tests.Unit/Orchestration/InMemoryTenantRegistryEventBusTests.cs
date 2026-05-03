using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Pins the concurrency + delivery semantics of the default
/// <see cref="ITenantRegistryEventBus"/> implementation. Wave 5.2.B and
/// 5.2.C both consume this bus; these tests exist so neither consumer has
/// to re-verify the contract at integration time.
/// </summary>
public class InMemoryTenantRegistryEventBusTests
{
    private static TenantLifecycleEvent MakeEvent(TenantStatus from = TenantStatus.Pending, TenantStatus to = TenantStatus.Active)
        => new(
            TenantId: Guid.NewGuid(),
            Previous: from,
            Current: to,
            OccurredAt: DateTimeOffset.UtcNow,
            Reason: null);

    [Fact]
    public void Publish_InvokesSubscriber()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        TenantLifecycleEvent? received = null;
        using var _ = bus.Subscribe(e => received = e);

        var evt = MakeEvent();
        bus.Publish(evt);

        Assert.NotNull(received);
        Assert.Equal(evt, received);
    }

    [Fact]
    public void Subscribe_ReturnsDisposable_UnsubscribedHandlerDoesNotFire()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var count = 0;

        var sub = bus.Subscribe(_ => count++);
        bus.Publish(MakeEvent());
        Assert.Equal(1, count);

        sub.Dispose();
        bus.Publish(MakeEvent());
        Assert.Equal(1, count); // still 1 — disposed handler silenced.
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var sub = bus.Subscribe(_ => { });
        sub.Dispose();
        sub.Dispose(); // must not throw.
    }

    [Fact]
    public void MultipleSubscribers_AllReceive()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var countA = 0;
        var countB = 0;
        var countC = 0;

        using var _a = bus.Subscribe(_ => countA++);
        using var _b = bus.Subscribe(_ => countB++);
        using var _c = bus.Subscribe(_ => countC++);

        bus.Publish(MakeEvent());

        Assert.Equal(1, countA);
        Assert.Equal(1, countB);
        Assert.Equal(1, countC);
    }

    [Fact]
    public void MisbehavingSubscriber_DoesNotBlockOthers()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var downstream = 0;

        using var _boom = bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var _ok = bus.Subscribe(_ => downstream++);

        bus.Publish(MakeEvent()); // must not throw.

        Assert.Equal(1, downstream);
    }

    [Fact]
    public void PublishFromWithinHandler_DoesNotDeadlock()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var outerFired = 0;
        var innerFired = 0;
        var recursed = false;

        using var _ = bus.Subscribe(e =>
        {
            outerFired++;
            if (!recursed)
            {
                recursed = true;
                bus.Publish(MakeEvent(TenantStatus.Active, TenantStatus.Suspended));
            }
            innerFired++;
        });

        bus.Publish(MakeEvent());

        Assert.Equal(2, outerFired);
        Assert.Equal(2, innerFired);
    }

    [Fact]
    public void SubscribeFromWithinHandler_DoesNotDeadlock_FiresOnNextPublish()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        var lateFired = 0;
        IDisposable? lateSub = null;

        using var _first = bus.Subscribe(_ =>
        {
            lateSub ??= bus.Subscribe(_ => lateFired++);
        });

        bus.Publish(MakeEvent()); // registers the late subscriber mid-iteration.
        Assert.Equal(0, lateFired); // snapshot semantics — late sub missed THIS publish.

        bus.Publish(MakeEvent());
        Assert.Equal(1, lateFired);

        lateSub?.Dispose();
    }

    [Fact]
    public void Publish_NullEvent_Throws()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Publish(null!));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var bus = new InMemoryTenantRegistryEventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Subscribe(null!));
    }
}

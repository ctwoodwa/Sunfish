using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// W#57 — coverage for
/// <see cref="InMemoryStandingOrderEventStream"/> per ADR 0065-A1
/// §A1.4. Pins the lock + list + subscriber-snapshot semantics
/// and the §A1.6 subscribe-then-replay dedup idiom.
/// </summary>
public sealed class StandingOrderEventStreamTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly ActorId ActorA = new("u1");

    private static StandingOrderAppliedEvent SampleEvent(
        TenantId? tenant = null,
        StandingOrderId? id = null) =>
        new(
            id ?? new StandingOrderId(Guid.NewGuid()),
            tenant ?? TenantA,
            ActorA,
            DateTimeOffset.UtcNow,
            StandingOrderScope.Tenant,
            new[]
            {
                new StandingOrderTriple("path", null, JsonNode.Parse("\"value\"")),
            },
            new AuditRecordId(Guid.NewGuid()),
            Rationale: "test");

    [Fact]
    public void ReplayAll_OnFreshStream_IsEmpty()
    {
        var stream = new InMemoryStandingOrderEventStream();
        Assert.Empty(stream.ReplayAll());
    }

    [Fact]
    public void ReplayAll_AfterPublish_ReturnsEventsInAppendOrder()
    {
        var stream = new InMemoryStandingOrderEventStream();
        var e1 = SampleEvent();
        var e2 = SampleEvent();
        var e3 = SampleEvent();

        stream.Publish(e1);
        stream.Publish(e2);
        stream.Publish(e3);

        var snap = stream.ReplayAll();
        Assert.Equal(3, snap.Count);
        Assert.Equal(e1.StandingOrderId, snap[0].StandingOrderId);
        Assert.Equal(e2.StandingOrderId, snap[1].StandingOrderId);
        Assert.Equal(e3.StandingOrderId, snap[2].StandingOrderId);
    }

    [Fact]
    public void Subscribe_FiresHandlerOnPublish()
    {
        var stream = new InMemoryStandingOrderEventStream();
        var received = new List<StandingOrderAppliedEvent>();
        using var _ = stream.Subscribe(received.Add);

        var evt = SampleEvent();
        stream.Publish(evt);

        Assert.Single(received);
        Assert.Equal(evt.StandingOrderId, received[0].StandingOrderId);
    }

    [Fact]
    public void Subscribe_DisposeStopsHandlerInvocations()
    {
        var stream = new InMemoryStandingOrderEventStream();
        var received = new List<StandingOrderAppliedEvent>();

        var sub = stream.Subscribe(received.Add);
        stream.Publish(SampleEvent());
        sub.Dispose();
        stream.Publish(SampleEvent());

        Assert.Single(received);
    }

    [Fact]
    public void Subscribe_DisposeIsIdempotent()
    {
        var stream = new InMemoryStandingOrderEventStream();
        var sub = stream.Subscribe(_ => { });
        sub.Dispose();
        sub.Dispose(); // must not throw
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_AllReceiveEachEvent()
    {
        var stream = new InMemoryStandingOrderEventStream();
        var a = new List<StandingOrderAppliedEvent>();
        var b = new List<StandingOrderAppliedEvent>();

        using var _ = stream.Subscribe(a.Add);
        using var __ = stream.Subscribe(b.Add);

        var evt = SampleEvent();
        stream.Publish(evt);

        Assert.Single(a);
        Assert.Single(b);
        Assert.Equal(evt.StandingOrderId, a[0].StandingOrderId);
        Assert.Equal(evt.StandingOrderId, b[0].StandingOrderId);
    }

    [Fact]
    public void Subscribe_DisposedMidFanout_DoesNotPreventOtherSubscribers()
    {
        // The subscriber-snapshot pattern means the snapshot is taken
        // under lock before fanout; a subscriber disposed during a
        // synchronous handler does not prevent siblings from being
        // invoked from the same publish call.
        var stream = new InMemoryStandingOrderEventStream();
        IDisposable? subA = null;
        var b = new List<StandingOrderAppliedEvent>();

        subA = stream.Subscribe(_ => subA!.Dispose());
        using var subB = stream.Subscribe(b.Add);

        stream.Publish(SampleEvent());

        Assert.Single(b);
    }

    [Fact]
    public void SubscribeThenReplay_DedupPattern_ConvergesExactlyOnce()
    {
        // §A1.6 catch-up cache idiom: subscribe first, then replay,
        // dedup on StandingOrderId via HashSet.
        var stream = new InMemoryStandingOrderEventStream();

        var seen = new HashSet<StandingOrderId>();
        var liveCount = 0;
        var replayCount = 0;

        // Pre-publish 2 historical events.
        var historical1 = SampleEvent();
        var historical2 = SampleEvent();
        stream.Publish(historical1);
        stream.Publish(historical2);

        using var _ = stream.Subscribe(evt =>
        {
            if (seen.Add(evt.StandingOrderId)) liveCount++;
        });

        // Race: a new event lands BEFORE replay below.
        var concurrent = SampleEvent();
        stream.Publish(concurrent);

        foreach (var evt in stream.ReplayAll())
        {
            if (seen.Add(evt.StandingOrderId)) replayCount++;
        }

        // Final state: every distinct event seen exactly once.
        Assert.Equal(3, seen.Count);
        Assert.Equal(historical1.StandingOrderId, stream.ReplayAll()[0].StandingOrderId);
        Assert.Equal(historical2.StandingOrderId, stream.ReplayAll()[1].StandingOrderId);
        Assert.Equal(concurrent.StandingOrderId, stream.ReplayAll()[2].StandingOrderId);
        // The subscriber-then-replay pattern guarantees the union of
        // (live observed before replay) + (replay-observed) equals every
        // published event with no duplicates.
        Assert.Equal(3, liveCount + replayCount);
    }

    [Fact]
    public void ReplayThenSubscribe_LosesEventsInGap_DemonstratesWhyA16InvertsTheOrder()
    {
        // Companion to SubscribeThenReplay_DedupPattern_ConvergesExactlyOnce.
        // The §A1.6 idiom is "subscribe FIRST, then replay" — this test
        // demonstrates the failure mode of the wrong order: an event
        // landing in the gap between ReplayAll and Subscribe is missed
        // by both halves of a naïve catch-up cache.
        var stream = new InMemoryStandingOrderEventStream();
        var historical = SampleEvent();
        stream.Publish(historical);

        var seen = new HashSet<StandingOrderId>();

        // Wrong order: replay first…
        foreach (var evt in stream.ReplayAll())
        {
            seen.Add(evt.StandingOrderId);
        }
        // …then a publish lands in the gap…
        var inGap = SampleEvent();
        stream.Publish(inGap);
        // …then subscribe.
        using var _ = stream.Subscribe(evt => seen.Add(evt.StandingOrderId));

        // Final state: only `historical` is in `seen`. The `inGap`
        // event landed AFTER ReplayAll snapshotted its state and
        // BEFORE Subscribe registered the handler — both halves
        // missed it. ReplayAll() now contains it, but the consumer
        // already iterated their stale snapshot.
        Assert.Single(seen);
        Assert.Contains(historical.StandingOrderId, seen);
        Assert.DoesNotContain(inGap.StandingOrderId, seen);

        // Confirms §A1.6 inverts the order specifically to close this
        // gap — subscribe-first means the live handler catches the
        // gap-window publish; replay-then-dedup catches the
        // before-subscribe publishes.
    }

    [Fact]
    public void Subscribe_TenantFilterPattern_IgnoresOffTenantEvents()
    {
        // §A1.6 tenant-scope filter: callers MUST filter on TenantId
        // when their concern is tenant-scoped. The stream is
        // intentionally all-tenant.
        var stream = new InMemoryStandingOrderEventStream();
        var tenantAEvents = new List<StandingOrderAppliedEvent>();

        using var _ = stream.Subscribe(evt =>
        {
            if (evt.TenantId.Equals(TenantA))
            {
                tenantAEvents.Add(evt);
            }
        });

        stream.Publish(SampleEvent(TenantA));
        stream.Publish(SampleEvent(TenantB));
        stream.Publish(SampleEvent(TenantA));

        Assert.Equal(2, tenantAEvents.Count);
        Assert.All(tenantAEvents, evt => Assert.Equal(TenantA, evt.TenantId));
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        var stream = new InMemoryStandingOrderEventStream();
        Assert.Throws<ArgumentNullException>(() => stream.Subscribe(null!));
    }

    [Fact]
    public async Task ConcurrentPublishAndSubscribe_DoesNotDeadlock()
    {
        // Smoke test: 100 concurrent publishes + 50 concurrent
        // subscribe-and-disposes complete inside a 5-second budget.
        var stream = new InMemoryStandingOrderEventStream();

        var publishers = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            stream.Publish(SampleEvent()))).ToArray();

        var subscribers = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            using var _ = stream.Subscribe(_ => { });
        })).ToArray();

        var all = publishers.Concat(subscribers).ToArray();
        var done = Task.WhenAll(all);
        var winner = await Task.WhenAny(done, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(done, winner);
        Assert.Equal(100, stream.ReplayAll().Count);
    }
}

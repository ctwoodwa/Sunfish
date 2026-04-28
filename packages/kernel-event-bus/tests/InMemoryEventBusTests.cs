using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.EventBus.Tests;

/// <summary>
/// Coverage for <see cref="InMemoryEventBus"/>: publish-path verification,
/// idempotent delivery, per-entity ordering, and subscription filtering.
/// </summary>
public class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_ValidSignedEvent_StoresInEntityChannel()
    {
        // Arrange — a real signer/verifier pair so the signature path is
        //           end-to-end rather than mocked; this also doubles as the
        //           closest-to-spec smoke test of the publish happy-path.
        var (bus, signer, verifier, keyPair) = CreateBus();
        var evt = BuildEvent("property:acme/1", "entity.created");
        var signed = await SignAsync(signer, evt);

        // Subscribe first so the fan-out reaches us. Using a short ct to
        // bound the test.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<SignedOperation<KernelEvent>>();
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeAsync(new EventSubscription("sub-1"), cts.Token))
            {
                received.Add(e);
                if (received.Count == 1) break;
            }
        }, cts.Token);

        // Let the subscriber register before publishing — the bus only
        // fans out to subscribers present at the moment of publish.
        await WaitForSubscribersAsync(bus, expected: 1, cts.Token);

        // Act
        await bus.PublishAsync(signed, cts.Token);

        // Assert
        await subscribeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
        Assert.Equal(signed.Nonce, received[0].Nonce);
        Assert.True(verifier.Verify(received[0]));

        keyPair.Dispose();
    }

    [Fact]
    public async Task PublishAsync_InvalidSignature_ThrowsInvalidEvent()
    {
        // Arrange — sign a real envelope, then tamper with the payload so
        //           the signature no longer covers the bytes. Verify throws.
        var (bus, signer, _, keyPair) = CreateBus();
        var evt = BuildEvent("property:acme/1", "entity.created");
        var signed = await SignAsync(signer, evt);

        // Tamper: swap payload for a different one, signature no longer matches.
        var tampered = signed with { Payload = BuildEvent("property:acme/1", "entity.deleted") };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidEventException>(async () =>
            await bus.PublishAsync(tampered));

        keyPair.Dispose();
    }

    [Fact]
    public async Task PublishAsync_DuplicateNonce_SilentlyDiscarded()
    {
        // Arrange — two envelopes sharing the same nonce. The second publish
        //           must not fan out to subscribers.
        var (bus, signer, _, keyPair) = CreateBus();

        var evt1 = BuildEvent("property:acme/1", "entity.created");
        var signed1 = await SignAsync(signer, evt1);
        // Sign a different event body with the SAME nonce to simulate a
        // retry-with-same-nonce (idempotent publisher pattern).
        var evt2 = BuildEvent("property:acme/1", "entity.updated");
        var signed2 = await signer.SignAsync(evt2, DateTimeOffset.UtcNow, signed1.Nonce);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<SignedOperation<KernelEvent>>();
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeAsync(new EventSubscription("sub-1"), cts.Token))
            {
                received.Add(e);
            }
        }, cts.Token);
        await WaitForSubscribersAsync(bus, expected: 1, cts.Token);

        // Act
        await bus.PublishAsync(signed1, cts.Token);
        await bus.PublishAsync(signed2, cts.Token);

        // Wait a beat to let fan-out settle, then cancel subscribe. 500ms
        // (rather than 100ms) gives slow Windows CI runners enough headroom
        // for two PublishAsync writes + one channel-reader round-trip.
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        try { await subscribeTask; } catch (OperationCanceledException) { }

        // Assert — only the first publish should have reached the subscriber.
        Assert.Single(received);
        Assert.Equal(signed1.Nonce, received[0].Nonce);

        keyPair.Dispose();
    }

    [Fact]
    public async Task SubscribeAsync_PerEntityOrdering_PreservesPublishOrder_WithinOneEntity()
    {
        // Arrange — publish N events for the same EntityId back-to-back and
        //           assert the subscriber receives them in the exact order
        //           they were published. This is the core ordering guarantee
        //           documented in IEventBus xmldoc.
        const int n = 20;
        var (bus, signer, _, keyPair) = CreateBus();
        var entity = EntityId.Parse("property:acme/1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new List<KernelEvent>();
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeAsync(new EventSubscription("sub-1", EntityFilter: entity), cts.Token))
            {
                received.Add(e.Payload);
                if (received.Count == n) break;
            }
        }, cts.Token);
        await WaitForSubscribersAsync(bus, expected: 1, cts.Token);

        // Act
        var published = new List<KernelEvent>();
        for (var i = 0; i < n; i++)
        {
            var evt = new KernelEvent(
                Id: EventId.NewId(),
                EntityId: entity,
                Kind: "entity.updated",
                OccurredAt: DateTimeOffset.UtcNow,
                Payload: new Dictionary<string, object?> { ["seq"] = i });
            published.Add(evt);
            await bus.PublishAsync(await SignAsync(signer, evt), cts.Token);
        }

        // Assert
        await subscribeTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(n, received.Count);
        for (var i = 0; i < n; i++)
        {
            Assert.Equal(published[i].Id, received[i].Id);
            Assert.Equal(i, Assert.IsType<int>(received[i].Payload["seq"]));
        }

        keyPair.Dispose();
    }

    [Fact]
    public async Task SubscribeAsync_KindFilter_FiltersToMatchingEvents()
    {
        // Arrange — publish two kinds; subscribe with KindFilter set to one;
        //           assert only matching kind arrives.
        var (bus, signer, _, keyPair) = CreateBus();
        var entity = EntityId.Parse("property:acme/1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<KernelEvent>();
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeAsync(
                new EventSubscription("sub-filter", KindFilter: "entity.created"),
                cts.Token))
            {
                received.Add(e.Payload);
                if (received.Count == 2) break;
            }
        }, cts.Token);
        await WaitForSubscribersAsync(bus, expected: 1, cts.Token);

        // Act — one matching, one not, another matching.
        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), entity, "entity.created", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["n"] = 1 })), cts.Token);

        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), entity, "entity.updated", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["n"] = 2 })), cts.Token);

        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), entity, "entity.created", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["n"] = 3 })), cts.Token);

        // Assert
        await subscribeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, received.Count);
        Assert.All(received, e => Assert.Equal("entity.created", e.Kind));
        Assert.Equal(new[] { 1, 3 }, received.Select(e => Assert.IsType<int>(e.Payload["n"])));

        keyPair.Dispose();
    }

    [Fact]
    public async Task SubscribeAsync_EntityFilter_OnlyReceivesEventsForThatEntity()
    {
        // Arrange — publish to two entities, subscribe to only one.
        var (bus, signer, _, keyPair) = CreateBus();
        var wanted = EntityId.Parse("property:acme/1");
        var unwanted = EntityId.Parse("property:acme/2");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<KernelEvent>();
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in bus.SubscribeAsync(
                new EventSubscription("sub-e", EntityFilter: wanted),
                cts.Token))
            {
                received.Add(e.Payload);
                if (received.Count == 2) break;
            }
        }, cts.Token);
        await WaitForSubscribersAsync(bus, expected: 1, cts.Token);

        // Act
        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), wanted, "entity.created", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["tag"] = "A" })), cts.Token);

        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), unwanted, "entity.created", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["tag"] = "NOPE" })), cts.Token);

        await bus.PublishAsync(await SignAsync(signer, new KernelEvent(
            EventId.NewId(), wanted, "entity.updated", DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["tag"] = "B" })), cts.Token);

        // Assert
        await subscribeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, received.Count);
        Assert.All(received, e => Assert.Equal(wanted, e.EntityId));
        Assert.Equal(new[] { "A", "B" }, received.Select(e => Assert.IsType<string>(e.Payload["tag"])));

        keyPair.Dispose();
    }

    // ----- helpers -----

    /// <summary>
    /// Creates a bus wired to a fresh Ed25519 signer/verifier pair. The
    /// returned <see cref="KeyPair"/> wraps the unmanaged NSec key — callers
    /// dispose it after they are done with the signer.
    /// </summary>
    private static (InMemoryEventBus bus, Ed25519Signer signer, Ed25519Verifier verifier, KeyPair keyPair) CreateBus()
    {
        var verifier = new Ed25519Verifier();
        var keyPair = KeyPair.Generate();
        var signer = new Ed25519Signer(keyPair);
        var bus = new InMemoryEventBus(verifier);
        return (bus, signer, verifier, keyPair);
    }

    private static KernelEvent BuildEvent(string entityId, string kind)
        => new(
            Id: EventId.NewId(),
            EntityId: EntityId.Parse(entityId),
            Kind: kind,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: new Dictionary<string, object?> { ["v"] = 1 });

    private static ValueTask<SignedOperation<KernelEvent>> SignAsync(Ed25519Signer signer, KernelEvent evt)
        => signer.SignAsync(evt, DateTimeOffset.UtcNow, Guid.NewGuid());

    /// <summary>
    /// Polls <see cref="InMemoryEventBus.SubscriberCount"/> until at least
    /// <paramref name="expected"/> subscribers are registered. Replaces the
    /// flake-prone <c>await Task.Delay(50, cts.Token)</c> "give the iterator
    /// time to register" pattern with a deterministic wait — registration is
    /// synchronous inside the subscribe iterator body, so polling the count is
    /// race-free.
    /// </summary>
    private static async Task WaitForSubscribersAsync(InMemoryEventBus bus, int expected, CancellationToken ct)
    {
        while (bus.SubscriberCount < expected)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(5, ct);
        }
    }
}

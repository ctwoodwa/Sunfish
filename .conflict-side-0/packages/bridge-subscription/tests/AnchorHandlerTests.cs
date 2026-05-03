using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class AnchorHandlerTests
{
    private const string TenantA = "tenant-a";
    private const string Secret = "shared-secret-base64";
    private const string SourceIp = "203.0.113.42";
    private static readonly TenantId AuditTenant = new("tenant-a");

    private static async Task<BridgeSubscriptionEvent> NewSignedEventAsync(string secret = Secret) =>
        await new HmacSha256EventSigner().SignAsync(new BridgeSubscriptionEvent
        {
            TenantId = TenantA,
            EventType = BridgeSubscriptionEventType.SubscriptionTierUpgraded,
            EditionBefore = "anchor-self-host",
            EditionAfter = "bridge-pro",
            EffectiveAt = DateTimeOffset.UtcNow,
            EventId = Guid.NewGuid(),
            DeliveryAttempt = 1,
            Signature = string.Empty,
        }, secret);

    private static async Task<(InMemoryBridgeSubscriptionEventHandler handler, ISharedSecretStore secretStore, IIdempotencyCache cache, FakeTime time)> NewHandlerAsync(string secret = Secret)
    {
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, secret);
        var cache = new InMemoryIdempotencyCache();
        var time = new FakeTime(DateTimeOffset.UtcNow);
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            cache,
            replayWindow: new ReplayWindow(),
            time: time);
        return (handler, secretStore, cache, time);
    }

    [Fact]
    public async Task HandleAsync_ValidEvent_ReturnsOk()
    {
        var (handler, _, _, _) = await NewHandlerAsync();
        var evt = await NewSignedEventAsync();
        var status = await handler.HandleAsync(evt, SourceIp);
        Assert.Equal(HandlerResponseStatus.Ok, status);
    }

    [Fact]
    public async Task HandleAsync_TamperedSignature_ReturnsSignatureFailed()
    {
        var (handler, _, _, _) = await NewHandlerAsync();
        var evt = await NewSignedEventAsync();
        var tampered = evt with { EditionAfter = "tampered-pro" };
        var status = await handler.HandleAsync(tampered, SourceIp);
        Assert.Equal(HandlerResponseStatus.SignatureFailed, status);
    }

    [Fact]
    public async Task HandleAsync_WrongSecret_ReturnsSignatureFailed()
    {
        var (handler, _, _, _) = await NewHandlerAsync(secret: "the-real-secret");
        // Sign with a different secret.
        var evt = await NewSignedEventAsync(secret: "wrong-secret");
        var status = await handler.HandleAsync(evt, SourceIp);
        Assert.Equal(HandlerResponseStatus.SignatureFailed, status);
    }

    [Fact]
    public async Task HandleAsync_SecretWithinGraceWindow_VerifiesAgainstPrevious()
    {
        var time = new FakeTime(DateTimeOffset.UtcNow);
        var secretStore = new InMemorySharedSecretStore(time);
        await secretStore.StageRotationAsync(TenantA, "old-secret");
        time.Advance(TimeSpan.FromDays(89));
        await secretStore.StageRotationAsync(TenantA, "new-secret");

        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            time: time);

        // An event signed with the OLD secret arrives during the grace window.
        var signed = await new HmacSha256EventSigner().SignAsync(new BridgeSubscriptionEvent
        {
            TenantId = TenantA,
            EventType = BridgeSubscriptionEventType.SubscriptionRenewed,
            EditionBefore = "x", EditionAfter = "x",
            EffectiveAt = time.GetUtcNow(),
            EventId = Guid.NewGuid(),
            DeliveryAttempt = 1,
            Signature = string.Empty,
        }, "old-secret");

        var status = await handler.HandleAsync(signed, SourceIp);
        Assert.Equal(HandlerResponseStatus.Ok, status);
    }

    [Fact]
    public async Task HandleAsync_StaleEvent_ReturnsStale()
    {
        var (handler, _, _, time) = await NewHandlerAsync();
        var oldEffectiveAt = time.GetUtcNow().AddMinutes(-10);
        var unsigned = new BridgeSubscriptionEvent
        {
            TenantId = TenantA,
            EventType = BridgeSubscriptionEventType.SubscriptionDunning,
            EditionBefore = "x", EditionAfter = "x",
            EffectiveAt = oldEffectiveAt,
            EventId = Guid.NewGuid(),
            DeliveryAttempt = 1,
            Signature = string.Empty,
        };
        var signed = await new HmacSha256EventSigner().SignAsync(unsigned, Secret);

        var status = await handler.HandleAsync(signed, SourceIp);
        Assert.Equal(HandlerResponseStatus.Stale, status);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_ReturnsAlreadyProcessed()
    {
        var (handler, _, _, _) = await NewHandlerAsync();
        var evt = await NewSignedEventAsync();
        var first = await handler.HandleAsync(evt, SourceIp);
        var second = await handler.HandleAsync(evt, SourceIp);
        Assert.Equal(HandlerResponseStatus.Ok, first);
        Assert.Equal(HandlerResponseStatus.AlreadyProcessed, second);
    }

    [Fact]
    public async Task HandleAsync_InvokesEditionCacheUpdater_OnSuccess()
    {
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, Secret);
        var updater = Substitute.For<IEditionCacheUpdater>();
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            editionCacheUpdater: updater);

        var evt = await NewSignedEventAsync();
        await handler.HandleAsync(evt, SourceIp);

        await updater.Received(1).ApplyAsync(Arg.Any<BridgeSubscriptionEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DoesNotInvokeUpdater_OnSignatureFailure()
    {
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, Secret);
        var updater = Substitute.For<IEditionCacheUpdater>();
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            editionCacheUpdater: updater);

        var evt = await NewSignedEventAsync();
        var tampered = evt with { Signature = "hmac-sha256:xxxxxxxxxxxxxxxxxx" };
        var status = await handler.HandleAsync(tampered, SourceIp);

        Assert.Equal(HandlerResponseStatus.SignatureFailed, status);
        await updater.DidNotReceive().ApplyAsync(Arg.Any<BridgeSubscriptionEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AuditEnabled_EmitsReceivedOnSuccess()
    {
        var trail = Substitute.For<IAuditTrail>();
        var operationSigner = new Ed25519Signer(KeyPair.Generate());
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, Secret);
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            trail, operationSigner, AuditTenant);

        var evt = await NewSignedEventAsync();
        await handler.HandleAsync(evt, SourceIp);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.BridgeSubscriptionEventReceived)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AuditEnabled_EmitsSignatureFailedOnTampered()
    {
        var trail = Substitute.For<IAuditTrail>();
        var operationSigner = new Ed25519Signer(KeyPair.Generate());
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, Secret);
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            trail, operationSigner, AuditTenant);

        var evt = await NewSignedEventAsync();
        var tampered = evt with { EditionAfter = "tampered" };
        await handler.HandleAsync(tampered, SourceIp);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.BridgeSubscriptionEventSignatureFailed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AuditEnabled_EmitsStaleOnReplay()
    {
        var trail = Substitute.For<IAuditTrail>();
        var operationSigner = new Ed25519Signer(KeyPair.Generate());
        var time = new FakeTime(DateTimeOffset.UtcNow);
        var secretStore = new InMemorySharedSecretStore();
        await secretStore.StageRotationAsync(TenantA, Secret);
        var handler = new InMemoryBridgeSubscriptionEventHandler(
            new HmacSha256EventSigner(),
            secretStore,
            new InMemoryIdempotencyCache(),
            trail, operationSigner, AuditTenant,
            time: time);

        var staleEffective = time.GetUtcNow().AddMinutes(-10);
        var unsigned = new BridgeSubscriptionEvent
        {
            TenantId = TenantA,
            EventType = BridgeSubscriptionEventType.SubscriptionDunning,
            EditionBefore = "x", EditionAfter = "x",
            EffectiveAt = staleEffective,
            EventId = Guid.NewGuid(),
            DeliveryAttempt = 1,
            Signature = string.Empty,
        };
        var signed = await new HmacSha256EventSigner().SignAsync(unsigned, Secret);
        await handler.HandleAsync(signed, SourceIp);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.BridgeSubscriptionEventStale)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var ops = new Ed25519Signer(KeyPair.Generate());
        var signer = new HmacSha256EventSigner();
        var store = new InMemorySharedSecretStore();
        var cache = new InMemoryIdempotencyCache();

        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryBridgeSubscriptionEventHandler(signer, store, cache, null!, ops, AuditTenant));
        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryBridgeSubscriptionEventHandler(signer, store, cache, trail, null!, AuditTenant));
        Assert.Throws<ArgumentException>(() =>
            new InMemoryBridgeSubscriptionEventHandler(signer, store, cache, trail, ops, default));
    }

    [Fact]
    public async Task HandleAsync_NullArgs_Throw()
    {
        var (handler, _, _, _) = await NewHandlerAsync();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.HandleAsync(null!, SourceIp).AsTask());
        var evt = await NewSignedEventAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(evt, string.Empty).AsTask());
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}

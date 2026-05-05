using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

public class DefaultOodWatchServiceTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly ActorId OnWatch = new("actor-on-watch");
    private static readonly ActorId Requester = new("actor-requester");
    private static readonly ActorId Incoming = new("actor-incoming");
    private static readonly DateTimeOffset T0 = new(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartWatch_Succeeds_EmitsAuditEvent()
    {
        var (svc, repo, audit, _, _) = NewService();
        var produced = NewWatch(state: OodWatchState.Active);
        repo.GetCurrentWatchAsync(Tenant, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns((OodWatch?)null);
        repo.StartWatchAsync(
                Tenant, OnWatch, OodRole.OfficerOfTheDeck, Arg.Any<TimeSpan?>(), Requester, Arg.Any<CancellationToken>())
            .Returns(produced);

        var result = await svc.StartWatchAsync(
            Tenant, OnWatch, OodRole.OfficerOfTheDeck,
            TimeSpan.FromHours(8), Requester);

        Assert.Equal(produced, result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.OodWatchStarted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWatch_ConflictThrows_WhenActiveExists()
    {
        var (svc, repo, _, _, _) = NewService();
        var existing = NewWatch(state: OodWatchState.Active);
        repo.GetCurrentWatchAsync(Tenant, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns(existing);

        await Assert.ThrowsAsync<OodWatchConflictException>(
            () => svc.StartWatchAsync(
                Tenant, OnWatch, OodRole.OfficerOfTheDeck, null, Requester).AsTask());
    }

    [Fact]
    public async Task HandoverWatch_DelegatesAtomicallyToRepository()
    {
        // Council Finding 3: atomicity is owned by the repository's
        // HandoverWatchAsync; the service does not call RelieveWatchAsync +
        // StartWatchAsync separately.
        var (svc, repo, _, _, _) = NewService();
        var current = NewWatch(state: OodWatchState.Active);
        var relieved = current with { State = OodWatchState.Relieved, RelievedAt = T0, RelievedBy = Requester };
        var started = NewWatch(state: OodWatchState.Active, onWatch: Incoming);
        repo.HandoverWatchAsync(current.Id, Incoming, Requester, Arg.Any<CancellationToken>())
            .Returns((relieved, started));

        var (r, s) = await svc.HandoverWatchAsync(current.Id, Incoming, Requester, "shift change");

        Assert.Equal(relieved, r);
        Assert.Equal(started, s);
        await repo.Received(1).HandoverWatchAsync(
            current.Id, Incoming, Requester, Arg.Any<CancellationToken>());
        // Service does NOT call the separate Relieve/Start primitives.
        await repo.DidNotReceive().RelieveWatchAsync(
            Arg.Any<OodWatchId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().StartWatchAsync(
            Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<OodRole>(),
            Arg.Any<TimeSpan?>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandoverWatch_EmitsBothAuditEvents()
    {
        var (svc, repo, audit, _, _) = NewService();
        var current = NewWatch(state: OodWatchState.Active);
        var relieved = current with { State = OodWatchState.Relieved, RelievedAt = T0, RelievedBy = Requester };
        var started = NewWatch(state: OodWatchState.Active, onWatch: Incoming);
        repo.HandoverWatchAsync(current.Id, Incoming, Requester, Arg.Any<CancellationToken>())
            .Returns((relieved, started));

        await svc.HandoverWatchAsync(current.Id, Incoming, Requester, reason: null);

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.OodWatchRelieved),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.OodWatchStarted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartWatch_NullAuditTrail_ThrowsInvalidOperation()
    {
        // Council Finding 1: OOD-authority operations require IAuditTrail
        // + IOperationSigner. Service throws InvalidOperationException
        // rather than running authority ops with no audit trail.
        var repo = Substitute.For<IOodWatchRepository>();
        repo.GetCurrentWatchAsync(Tenant, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns((OodWatch?)null);
        repo.StartWatchAsync(
                Tenant, OnWatch, OodRole.OfficerOfTheDeck, Arg.Any<TimeSpan?>(), Requester, Arg.Any<CancellationToken>())
            .Returns(NewWatch(state: OodWatchState.Active));
        var svc = new DefaultOodWatchService(repo, auditTrail: null, signer: NewSigner(), timeProvider: new FakeTimeProvider(T0));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.StartWatchAsync(Tenant, OnWatch, OodRole.OfficerOfTheDeck, null, Requester).AsTask());
    }

    [Fact]
    public async Task GetActiveWatch_ReturnsNull_WhenNoneActive()
    {
        var (svc, repo, _, _, _) = NewService();
        repo.GetCurrentWatchAsync(Tenant, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns((OodWatch?)null);

        var result = await svc.GetActiveWatchAsync(Tenant, OodRole.OfficerOfTheDeck);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveWatch_ReturnsNull_ForExpiredWatch()
    {
        // Repository contract — returns null for non-Active watches, including
        // Expired. Service is a pass-through; null in, null out.
        var (svc, repo, _, _, _) = NewService();
        repo.GetCurrentWatchAsync(Tenant, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns((OodWatch?)null);

        var result = await svc.GetActiveWatchAsync(Tenant, OodRole.OfficerOfTheDeck);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandoverWatch_RepositoryThrowsConflict_PropagatesUnchanged()
    {
        // Repository owns atomicity + state validation. If the watch is not
        // Active, the repo throws OodWatchConflictException; the service
        // surfaces it without wrapping.
        var (svc, repo, _, _, _) = NewService();
        var watchId = OodWatchId.NewId();
        repo.HandoverWatchAsync(watchId, Incoming, Requester, Arg.Any<CancellationToken>())
            .Returns<(OodWatch, OodWatch)>(_ => throw new OodWatchConflictException(watchId, Tenant, OodRole.OfficerOfTheDeck));

        await Assert.ThrowsAsync<OodWatchConflictException>(
            () => svc.HandoverWatchAsync(watchId, Incoming, Requester, null).AsTask());
    }

    private static (DefaultOodWatchService svc, IOodWatchRepository repo, IAuditTrail audit, IOperationSigner signer, FakeTimeProvider time) NewService()
    {
        var repo = Substitute.For<IOodWatchRepository>();
        var audit = Substitute.For<IAuditTrail>();
        var signer = NewSigner();
        var time = new FakeTimeProvider(T0);
        var svc = new DefaultOodWatchService(repo, audit, signer, time);
        return (svc, repo, audit, signer, time);
    }

    private static OodWatch NewWatch(OodWatchState state, ActorId? onWatch = null) =>
        new(
            Id: OodWatchId.NewId(),
            TenantId: Tenant,
            OnWatchActor: onWatch ?? OnWatch,
            Role: OodRole.OfficerOfTheDeck,
            StartedAt: T0,
            RelievedAt: state == OodWatchState.Active ? null : T0.AddHours(1),
            StartedBy: Requester,
            RelievedBy: state == OodWatchState.Active ? (ActorId?)null : Requester,
            MaxWatchDuration: TimeSpan.FromHours(8),
            State: state);

    internal static IOperationSigner NewSigner()
    {
        var signer = Substitute.For<IOperationSigner>();
        var principalId = PrincipalId.FromBytes(new byte[32]);
        signer.IssuerId.Returns(principalId);
        signer.SignAsync(
            Arg.Any<AuditPayload>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var payload = call.Arg<AuditPayload>();
                var issuedAt = call.Arg<DateTimeOffset>();
                var nonce = call.Arg<Guid>();
                return new ValueTask<SignedOperation<AuditPayload>>(new SignedOperation<AuditPayload>(
                    Payload: payload!,
                    IssuerId: principalId,
                    IssuedAt: issuedAt,
                    Nonce: nonce,
                    Signature: Signature.FromBytes(new byte[64])));
            });
        return signer;
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FakeTimeProvider(DateTimeOffset start) { _now = start; }
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

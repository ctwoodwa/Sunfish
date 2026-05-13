using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.EngineRoom;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.EngineRoom.Tests;

/// <summary>
/// W#50 Phase 2b — coverage for
/// <see cref="DefaultEngineRoomCommandService"/> per ADR 0079 §2 +
/// §Trust audit-emission ordering invariant.
/// </summary>
public class DefaultEngineRoomCommandServiceTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly ActorId ActorA = new("alice");
    private const string DocId = "doc:01HV";
    private const string Reason = "suspected corruption";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IOperationSigner NewSigner()
    {
        var principalId = PrincipalId.FromBytes(new byte[32]);
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(principalId);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var payload = call.Arg<AuditPayload>();
                var occurredAt = call.Arg<DateTimeOffset>();
                var nonce = call.Arg<Guid>();
                return new ValueTask<SignedOperation<AuditPayload>>(
                    new SignedOperation<AuditPayload>(
                        Payload: payload!,
                        IssuerId: principalId,
                        IssuedAt: occurredAt,
                        Nonce: nonce,
                        Signature: Signature.FromBytes(new byte[64])));
            });
        return signer;
    }

    private static IActorPrincipalResolver ResolverFor(ActorId actor)
    {
        var principal = new Individual(PrincipalId.FromBytes(new byte[32]));
        var resolver = new InMemoryActorPrincipalResolver();
        resolver.Register(actor, principal);
        return resolver;
    }

    private static IPermissionResolver AlwaysGrant()
    {
        var resolver = Substitute.For<IPermissionResolver>();
        resolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<PermissionDecision>(
                new PermissionDecision.Granted(
                    ShipRole.Captain,
                    DateTimeOffset.UtcNow,
                    Proof: null)));
        return resolver;
    }

    private static IPermissionResolver AlwaysDeny()
    {
        var resolver = Substitute.For<IPermissionResolver>();
        resolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<PermissionDecision>(
                new PermissionDecision.Denied(
                    DenialReason.NoMatchingRole,
                    "Role insufficient.",
                    new Remediation(
                        RemediationKind.ContactAuthority,
                        "Ask the Captain.",
                        ContactActor: null,
                        EscalationLink: null,
                        CallToActionLabel: null),
                    DateTimeOffset.UtcNow)));
        return resolver;
    }

    private static IOodWatchService EoowPresent()
    {
        var watch = new OodWatch(
            OodWatchId.NewId(),
            TenantA,
            ActorA,
            OodRole.EngineeringOfficerOfTheWatch,
            DateTimeOffset.UtcNow,
            RelievedAt: null,
            ActorA,
            RelievedBy: null,
            TimeSpan.FromHours(4),
            OodWatchState.Active);
        var svc = Substitute.For<IOodWatchService>();
        svc.GetActiveWatchAsync(Arg.Any<TenantId>(), Arg.Any<OodRole>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<OodWatch?>(watch));
        return svc;
    }

    private static IOodWatchService NoEoow()
    {
        var svc = Substitute.For<IOodWatchService>();
        svc.GetActiveWatchAsync(Arg.Any<TenantId>(), Arg.Any<OodRole>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<OodWatch?>(null));
        return svc;
    }

    private static IDocumentQuarantineStore SuccessfulStore()
    {
        var store = Substitute.For<IDocumentQuarantineStore>();
        store.QuarantineAsync(Arg.Any<string>(), Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new QuarantineResult(call.ArgAt<string>(0)!, DateTimeOffset.UtcNow)));
        store.ReleaseAsync(Arg.Any<string>(), Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new ReleaseResult(call.ArgAt<string>(0)!, DateTimeOffset.UtcNow)));
        store.CompactAsync(Arg.Any<string>(), Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(new CompactionResult(call.ArgAt<string>(0)!, BytesBefore: 1024, BytesAfter: 256, DateTimeOffset.UtcNow)));
        return store;
    }

    private static DefaultEngineRoomCommandService Build(
        IDocumentQuarantineStore? store = null,
        IActorPrincipalResolver? actorResolver = null,
        IPermissionResolver? permissionResolver = null,
        IOodWatchService? oodWatch = null,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null) =>
        new(
            store ?? SuccessfulStore(),
            actorResolver ?? ResolverFor(ActorA),
            permissionResolver ?? AlwaysGrant(),
            oodWatch ?? EoowPresent(),
            auditTrail ?? Substitute.For<IAuditTrail>(),
            signer ?? NewSigner());

    // ── Test cases ────────────────────────────────────────────────────────────

    /// <summary>
    /// Happy-path quarantine: QuarantineDocumentAsync returns the result from the
    /// store when the actor is authorized, emits pre-op + post-op audit events.
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_AuthorizedActor_ReturnsStoreResult_AndEmitsBothAuditEvents()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(auditTrail: audit);

        var result = await svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason);

        Assert.Equal(DocId, result.DocumentId);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantined)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Auth-denied: QuarantineDocumentAsync throws EngineRoomUnauthorizedException
    /// when the permission resolver denies. The denial audit MUST be emitted before
    /// the exception propagates. Pre-op DocumentQuarantineRequested MUST also have
    /// been emitted (FIRST observable side-effect per §Trust).
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_AuthDenied_Throws_AndEmitsDenialAudit()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(permissionResolver: AlwaysDeny(), auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason).AsTask());

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantined)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// §Trust ordering: pre-op DocumentQuarantineRequested MUST be appended to the
    /// audit trail BEFORE the actor resolver and permission resolver are consulted.
    /// Pins the invariant against refactors that might reorder for "performance."
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_PreOpAudit_OrderedBeforePermissionResolve()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        var principal = new Individual(PrincipalId.FromBytes(new byte[32]));
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(principal));
        var permResolver = AlwaysGrant();
        var svc = Build(actorResolver: actorResolver, permissionResolver: permResolver, auditTrail: audit);

        await svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason);

        Received.InOrder(() =>
        {
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
                Arg.Any<CancellationToken>());
            actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
            permResolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>());
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantined)),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// Compaction-ineligible: when the store throws InvalidOperationException,
    /// it MUST propagate as-is (NOT wrapped as EngineRoomUnauthorizedException)
    /// per the IEngineRoomCommandService contract.
    /// </summary>
    [Fact]
    public async Task CompactDocument_StoreThrowsInvalidOperation_PropagatesNotWrapped()
    {
        var store = Substitute.For<IDocumentQuarantineStore>();
        store.CompactAsync(Arg.Any<string>(), Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Document not eligible for compaction."));
        var svc = Build(store: store);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompactDocumentAsync(DocId, TenantA, ActorA).AsTask());

        Assert.IsNotType<EngineRoomUnauthorizedException>(ex);
        Assert.Contains("not eligible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// EOOW-null-watch: when no EOOW watch is active, the command MUST still
    /// succeed (non-blocking advisory). The store is called and the result
    /// is returned. No EngineRoomUnauthorizedException is thrown.
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_NoEoowWatch_ProceedsSuccessfully()
    {
        var store = SuccessfulStore();
        var svc = Build(store: store, oodWatch: NoEoow());

        var result = await svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason);

        Assert.Equal(DocId, result.DocumentId);
        await store.Received(1).QuarantineAsync(DocId, TenantA, ActorA, Reason, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// B2 (Blocking) — §Trust ordering on denial path: pre-op audit MUST be
    /// emitted BEFORE actor resolution, permission check, and denial audit.
    /// Pins the invariant against refactors that could swap denial-audit before
    /// pre-op.
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_AuthDenied_PreOpOrderedBeforeDenialAudit()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        var principal = new Individual(PrincipalId.FromBytes(new byte[32]));
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(principal));
        var permResolver = AlwaysDeny();
        var svc = Build(actorResolver: actorResolver, permissionResolver: permResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason).AsTask());

        Received.InOrder(() =>
        {
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
                Arg.Any<CancellationToken>());
            actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
            permResolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>());
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// B3 (Blocking) — actorResolver returning null is fail-closed:
    /// denial audit emitted, EngineRoomUnauthorizedException thrown,
    /// store NOT called, no post-op audit.
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_NullPrincipal_FailClosed_EmitsDenialAndThrows()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(null));
        var store = SuccessfulStore();
        var svc = Build(store: store, actorResolver: actorResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason).AsTask());

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().QuarantineAsync(Arg.Any<string>(), Arg.Any<TenantId>(),
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>R4 — ReleaseQuarantineAsync happy path: store called + both audit events emitted.</summary>
    [Fact]
    public async Task ReleaseQuarantine_AuthorizedActor_ReturnsStoreResult_AndEmitsBothAuditEvents()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(auditTrail: audit);

        var result = await svc.ReleaseQuarantineAsync(DocId, TenantA, ActorA, Reason);

        Assert.Equal(DocId, result.DocumentId);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineReleaseRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineReleased)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>R4 — CompactDocumentAsync happy path: store called + both audit events emitted.</summary>
    [Fact]
    public async Task CompactDocument_AuthorizedActor_ReturnsStoreResult_AndEmitsBothAuditEvents()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(auditTrail: audit);

        var result = await svc.CompactDocumentAsync(DocId, TenantA, ActorA);

        Assert.Equal(DocId, result.DocumentId);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.ManualCompactionInitiated)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.ManualCompactionCompleted)),
            Arg.Any<CancellationToken>());
    }

    // ── Council amendment: parity tests for Release + Compact denial and null-principal paths ──

    /// <summary>
    /// F7 — null-principal fail-closed parity for Release path.
    /// </summary>
    [Fact]
    public async Task ReleaseQuarantine_NullPrincipal_FailClosed_EmitsDenialAndThrows()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(null));
        var store = SuccessfulStore();
        var svc = Build(store: store, actorResolver: actorResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.ReleaseQuarantineAsync(DocId, TenantA, ActorA, Reason).AsTask());

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineReleaseRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().ReleaseAsync(Arg.Any<string>(), Arg.Any<TenantId>(),
            Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// F7 — null-principal fail-closed parity for Compact path.
    /// </summary>
    [Fact]
    public async Task CompactDocument_NullPrincipal_FailClosed_EmitsDenialAndThrows()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(null));
        var store = SuccessfulStore();
        var svc = Build(store: store, actorResolver: actorResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.CompactDocumentAsync(DocId, TenantA, ActorA).AsTask());

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.ManualCompactionInitiated)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().CompactAsync(Arg.Any<string>(), Arg.Any<TenantId>(),
            Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// F8 — §Trust ordering parity: pre-op BEFORE denial audit on Release path.
    /// </summary>
    [Fact]
    public async Task ReleaseQuarantine_AuthDenied_PreOpOrderedBeforeDenialAudit()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        var principal = new Individual(PrincipalId.FromBytes(new byte[32]));
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(principal));
        var permResolver = AlwaysDeny();
        var svc = Build(actorResolver: actorResolver, permissionResolver: permResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.ReleaseQuarantineAsync(DocId, TenantA, ActorA, Reason).AsTask());

        Received.InOrder(() =>
        {
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineReleaseRequested)),
                Arg.Any<CancellationToken>());
            actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
            permResolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>());
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// F8 — §Trust ordering parity: pre-op BEFORE denial audit on Compact path.
    /// </summary>
    [Fact]
    public async Task CompactDocument_AuthDenied_PreOpOrderedBeforeDenialAudit()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        var principal = new Individual(PrincipalId.FromBytes(new byte[32]));
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(principal));
        var permResolver = AlwaysDeny();
        var svc = Build(actorResolver: actorResolver, permissionResolver: permResolver, auditTrail: audit);

        await Assert.ThrowsAsync<EngineRoomUnauthorizedException>(
            () => svc.CompactDocumentAsync(DocId, TenantA, ActorA).AsTask());

        Received.InOrder(() =>
        {
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.ManualCompactionInitiated)),
                Arg.Any<CancellationToken>());
            actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
            permResolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>());
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DamageControlAuthorizationDenied)),
                Arg.Any<CancellationToken>());
        });
    }

    /// <summary>
    /// F1/F9 (Blocking) — pre-op audit storage failure aborts the operation before any
    /// store interaction. The §Trust invariant is: no forensic gap means no mutation.
    /// </summary>
    [Fact]
    public async Task QuarantineDocument_PreOpAuditStorageFails_DoesNotPersistAndPropagates()
    {
        var audit = Substitute.For<IAuditTrail>();
        audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DocumentQuarantineRequested)),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit storage unavailable"));
        var store = SuccessfulStore();
        var svc = Build(store: store, auditTrail: audit);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.QuarantineDocumentAsync(DocId, TenantA, ActorA, Reason).AsTask());

        await store.DidNotReceive().QuarantineAsync(
            Arg.Any<string>(), Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}

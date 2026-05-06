using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Quarterdeck.Tests;

/// <summary>
/// W#51 Phase 2b — coverage for
/// <see cref="DefaultQuarterdeckCommandService"/> per ADR 0080 §5
/// two-phase audit invariant.
/// </summary>
public class DefaultQuarterdeckCommandServiceTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly ActorId ActorA = new("alice");
    private const string AlertId = "sunfish.test:01HV4G7";

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

    private static IActorPrincipalResolver ResolverFor(ActorId actor, Principal principal)
    {
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

    private static DefaultQuarterdeckCommandService Build(
        IPermissionResolver permissionResolver,
        IAuditTrail auditTrail,
        IActorPrincipalResolver? actorResolver = null) =>
        new DefaultQuarterdeckCommandService(
            actorResolver ?? ResolverFor(ActorA, new Individual(PrincipalId.FromBytes(new byte[32]))),
            permissionResolver,
            auditTrail,
            NewSigner(),
            NullLogger<DefaultQuarterdeckCommandService>.Instance);

    [Fact]
    public async Task AcknowledgeAlert_GrantedPath_EmitsBothAuditEvents_AndReturnsTrue()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(AlwaysGrant(), audit);

        var result = await svc.AcknowledgeAlertAsync(AlertId, TenantA, ActorA);

        Assert.True(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested)),
            Arg.Any<CancellationToken>());
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledged)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcknowledgeAlert_DeniedPath_EmitsRequestedOnly_AndReturnsFalse()
    {
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(AlwaysDeny(), audit);

        var result = await svc.AcknowledgeAlertAsync(AlertId, TenantA, ActorA);

        Assert.False(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested)),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledged)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcknowledgeAlert_UnresolvableActor_EmitsRequestedOnly_AndReturnsFalse()
    {
        var emptyResolver = new InMemoryActorPrincipalResolver();
        var audit = Substitute.For<IAuditTrail>();
        var svc = Build(AlwaysGrant(), audit, actorResolver: emptyResolver);

        var result = await svc.AcknowledgeAlertAsync(
            AlertId, TenantA, new ActorId("not-a-canonical-key"));

        Assert.False(result);
        // Pre-op intent emitted as FIRST observable side-effect even
        // when the actor cannot be resolved (tenant-spoofing /
        // unknown-actor probing audit-trail discipline).
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested)),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledged)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcknowledgeAlert_NullAlertId_Throws()
    {
        var svc = Build(AlwaysGrant(), Substitute.For<IAuditTrail>());

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await svc.AcknowledgeAlertAsync(null!, TenantA, ActorA));
    }

    [Fact]
    public async Task AcknowledgeAlert_AuditPayload_ContainsAlertIdAndActor()
    {
        var audit = Substitute.For<IAuditTrail>();
        AuditRecord? captured = null;
        await audit.AppendAsync(Arg.Do<AuditRecord>(r =>
        {
            if (captured is null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested))
            {
                captured = r;
            }
        }), Arg.Any<CancellationToken>());

        var svc = Build(AlwaysGrant(), audit);
        await svc.AcknowledgeAlertAsync(AlertId, TenantA, ActorA);

        Assert.NotNull(captured);
        var body = captured!.Payload.Payload.Body;
        Assert.True(body.ContainsKey("alert_id"));
        Assert.Equal(AlertId, body["alert_id"]);
        Assert.True(body.ContainsKey("actor"));
        Assert.Equal(ActorA.Value, body["actor"]);
    }

    [Fact]
    public async Task AcknowledgeAlert_GrantedAuditPayload_ContainsGrantedTrue()
    {
        var audit = Substitute.For<IAuditTrail>();
        AuditRecord? captured = null;
        await audit.AppendAsync(Arg.Do<AuditRecord>(r =>
        {
            if (r.EventType.Equals(AuditEventType.AlertAcknowledged))
            {
                captured = r;
            }
        }), Arg.Any<CancellationToken>());

        var svc = Build(AlwaysGrant(), audit);
        await svc.AcknowledgeAlertAsync(AlertId, TenantA, ActorA);

        Assert.NotNull(captured);
        var body = captured!.Payload.Payload.Body;
        Assert.True(body.ContainsKey("granted"));
        Assert.Equal(true, body["granted"]);
    }

    /// <summary>
    /// Cross-tenant probe: actor is registered in the in-memory
    /// resolver (which ignores tenantId per IActorPrincipalResolver
    /// xmldoc), but the target tenant has no role for that principal.
    /// IPermissionResolver gates the call → deny. Pre-op intent record
    /// MUST still emit so the probe is auditable. Pins the §5.2
    /// tenant-binding invariant from the W#51 hand-off.
    /// </summary>
    [Fact]
    public async Task AcknowledgeAlert_CrossTenantProbe_DeniesViaPermissionResolver_AndEmitsRequested()
    {
        var foreignActor = new ActorId("bob-from-tenant-beta");
        var foreignPrincipal = new Individual(PrincipalId.FromBytes(new byte[32]));
        var resolver = new InMemoryActorPrincipalResolver();
        resolver.Register(foreignActor, foreignPrincipal);

        var audit = Substitute.For<IAuditTrail>();
        // AlwaysDeny simulates "no role at TenantA for foreignPrincipal".
        var svc = Build(AlwaysDeny(), audit, actorResolver: resolver);

        var result = await svc.AcknowledgeAlertAsync(AlertId, TenantA, foreignActor);

        Assert.False(result);
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested)),
            Arg.Any<CancellationToken>());
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledged)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// §5 FIRST-observable-side-effect ordering: pre-op
    /// AlertAcknowledgementRequested MUST be appended to the audit
    /// trail BEFORE the actor and permission resolvers are consulted.
    /// Pins the invariant against future refactors that might lift
    /// the actor/permission lookups above the audit emission for
    /// "performance."
    /// </summary>
    [Fact]
    public async Task AcknowledgeAlert_GrantedPath_EmitsRequested_BeforeActorAndPermissionResolution()
    {
        var audit = Substitute.For<IAuditTrail>();
        var actorResolver = Substitute.For<IActorPrincipalResolver>();
        actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Principal?>(new Individual(PrincipalId.FromBytes(new byte[32]))));
        var permissionResolver = AlwaysGrant();

        var svc = new DefaultQuarterdeckCommandService(
            actorResolver,
            permissionResolver,
            audit,
            NewSigner(),
            NullLogger<DefaultQuarterdeckCommandService>.Instance);

        await svc.AcknowledgeAlertAsync(AlertId, TenantA, ActorA);

        Received.InOrder(() =>
        {
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledgementRequested)),
                Arg.Any<CancellationToken>());
            actorResolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>());
            permissionResolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>());
            audit.AppendAsync(
                Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AlertAcknowledged)),
                Arg.Any<CancellationToken>());
        });
    }
}

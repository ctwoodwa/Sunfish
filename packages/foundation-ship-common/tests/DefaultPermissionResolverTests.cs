using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Ship.Common.Tests;

public class DefaultPermissionResolverTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly DateTimeOffset T0 = new(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeckCanonicalization_PromotesMainDeckToBelowTheWaterline_ForQuarantine()
    {
        // §2.1 step 0(a): caller passing MainDeck for Quarantine is silently
        // promoted to BelowTheWaterline; only Captain/XO can act there.
        var (resolver, audit, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();
        var resource = new Resource("inv-1");

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Tactical, DeckDepth.MainDeck,
            ShipAction.Quarantine, resource);

        Assert.IsType<PermissionDecision.Granted>(decision);
        // No audit emission for routine grants (audit-loud sets are noted
        // but Phase 1 does not emit grant-loud records).
        await audit.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.PermissionDenied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeckCanonicalization_DivisionOfficer_DeniedQuarantine_AtBelowTheWaterline()
    {
        // §2.1 step 5: Division Officer is not Captain/XO, so a
        // BelowTheWaterline action returns DeckRestriction.
        var (resolver, audit, _) = NewResolver(WithDivisionOfficer());
        var actor = NewPrincipal();
        var resource = new Resource("inv-1");

        var decision = await resolver.ResolveAsync(
            actor, ShipLocation.Tactical, DeckDepth.MainDeck,
            ShipAction.Quarantine, resource);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.DeckRestriction, denied.Reason);
        Assert.False(string.IsNullOrEmpty(denied.ReasonDisplay));
        Assert.NotNull(denied.Remediation);
    }

    [Fact]
    public async Task PromotionGuard_SelfPromotion_Denied()
    {
        // §2.1 step 0(b): self-promotion returns SecurityPolicyBlocked
        // unconditionally regardless of hierarchy position.
        var actor = new ActorId("actor-1");
        var denied = DefaultPermissionResolver.CheckPromotionGuard(
            ShipRole.Captain, actor, actor, ShipRole.XO, T0);

        Assert.NotNull(denied);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied!.Reason);
        Assert.Contains("self-promotion", denied.ReasonDisplay);
    }

    [Fact]
    public async Task PromotionGuard_HierarchyInversion_Denied()
    {
        // §2.1 step 0(b): caller's role MUST be strictly higher than
        // target's role; equal-rank promotion is forbidden.
        var actorA = new ActorId("actor-a");
        var actorB = new ActorId("actor-b");
        var denied = DefaultPermissionResolver.CheckPromotionGuard(
            ShipRole.DivisionOfficer, actorA, actorB, ShipRole.Captain, T0);

        Assert.NotNull(denied);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied!.Reason);
        Assert.Contains("insufficient authority", denied.ReasonDisplay);
    }

    [Fact]
    public async Task PromotionGuard_ValidPromotion_ReturnsNull()
    {
        // Captain promoting a DivisionOfficer to XO is hierarchy-valid.
        var actorA = new ActorId("actor-a");
        var actorB = new ActorId("actor-b");
        var denied = DefaultPermissionResolver.CheckPromotionGuard(
            ShipRole.Captain, actorA, actorB, ShipRole.XO, T0);

        Assert.Null(denied);
    }

    [Fact]
    public async Task ResourceScopeGuard_NullResourceForApprove_Denied()
    {
        // §2.1 step 0(c): resource-scoped action requires non-null resource.
        var (resolver, audit, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.MainDeck,
            ShipAction.Approve, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied.Reason);
        Assert.Contains("resource-scoped", denied.ReasonDisplay);
    }

    [Fact]
    public async Task WatchPrecondition_StandWatch_ReturnsWatchRequired()
    {
        // §2.1 step 1: StandWatch requires OOD/EOOW designation. Phase 1
        // ships without IOnWatchProbe wired so all watch-required calls
        // return WatchRequired.
        var (resolver, audit, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.MainDeck,
            ShipAction.StandWatch, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.WatchRequired, denied.Reason);
        Assert.Equal(RemediationKind.AwaitWatch, denied.Remediation.Kind);
    }

    [Fact]
    public async Task DeferralCheck_SupplyOffice_Phase2Deferred()
    {
        // §2.1 step 3: SupplyOffice short-circuits to Phase2Deferred.
        var (resolver, _, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.SupplyOffice, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.Phase2Deferred, denied.Reason);
        Assert.Equal(RemediationKind.Phase2Deferred, denied.Remediation.Kind);
    }

    [Fact]
    public async Task DeferralCheck_Wardroom_V2Deferred()
    {
        // §2.1 step 3: Wardroom short-circuits to V2Deferred.
        var (resolver, _, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Wardroom, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.V2Deferred, denied.Reason);
    }

    [Fact]
    public async Task RoleMatch_NoAssignment_Denied_NoMatchingRole()
    {
        // §2.1 step 4: subject without an assignment in any tenant gets
        // NoMatchingRole.
        var (resolver, _, source) = NewResolver(assignments: Array.Empty<ShipRoleAssignment>());
        source.ResolveAssignmentAsync(Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ShipRoleAssignment?>(null));
        var stranger = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            stranger, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.NoMatchingRole, denied.Reason);
    }

    [Fact]
    public async Task RoleMatch_SUPPO_Phase2Deferred_PerSection16()
    {
        // §1.6: SUPPO is structurally valid but operationally inert.
        var (resolver, _, _) = NewResolver(WithRole(ShipRole.SUPPO));
        var actor = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            actor, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.Phase2Deferred, denied.Reason);
    }

    [Fact]
    public async Task LocationScope_DivisionOfficerInSickBay_LocationOutOfScope()
    {
        // Division Officer is excluded from SickBay (medical specialist
        // territory).
        var (resolver, _, _) = NewResolver(WithDivisionOfficer());
        var actor = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            actor, ShipLocation.SickBay, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.LocationOutOfScope, denied.Reason);
    }

    [Fact]
    public async Task DeniedDecision_AccessibilityShape_NonEmptyDisplays()
    {
        // §2.3: every Denied carries non-null + non-empty ReasonDisplay
        // and a non-null Remediation with non-null GuidanceDisplay.
        var (resolver, _, _) = NewResolver(assignments: Array.Empty<ShipRoleAssignment>());
        var stranger = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            stranger, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.False(string.IsNullOrEmpty(denied.ReasonDisplay));
        Assert.NotNull(denied.Remediation);
        Assert.False(string.IsNullOrEmpty(denied.Remediation.GuidanceDisplay));
    }

    [Fact]
    public async Task DenialEmitsAuditRecord()
    {
        // §2.4: every Denied decision emits PermissionDenied.
        var (resolver, audit, _) = NewResolver(WithDivisionOfficer());
        var actor = NewPrincipal();

        await resolver.ResolveAsync(
            actor, ShipLocation.SickBay, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.PermissionDenied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantedDecision_DoesNotEmit_PermissionDenied()
    {
        // §2.4: Granted decisions are NOT audit-loud by default in Phase 1.
        var (resolver, audit, _) = NewResolver(WithCaptain());
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        Assert.IsType<PermissionDecision.Granted>(decision);
        await audit.DidNotReceive().AppendAsync(
            Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RateLimit_AfterTenDenials_EmitsRateExceededOnce_AndShortCircuits()
    {
        // §2.4: 11th call in the same window emits PermissionDenialRateExceeded
        // exactly once; subsequent calls within the window short-circuit.
        var (resolver, audit, _) = NewResolver(WithDivisionOfficer());
        var actor = NewPrincipal();

        // Call 11 times (same actor + location): each Denied(LocationOutOfScope).
        for (var i = 0; i < 11; i++)
        {
            await resolver.ResolveAsync(
                actor, ShipLocation.SickBay, DeckDepth.MainDeck,
                ShipAction.Read, resource: null);
        }

        // 12th call: short-circuit — counter (11) > limit (10).
        var decision = await resolver.ResolveAsync(
            actor, ShipLocation.SickBay, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied.Reason);
        Assert.Contains("rate limit", denied.ReasonDisplay);

        // PermissionDenialRateExceeded emitted exactly once.
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.PermissionDenialRateExceeded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CacheTtl_AfterExpiry_ReloadsFromSource()
    {
        // §2.5 (Phase 1 fallback): per-tenant cache reloads after 60s.
        var assignments = WithCaptain();
        var time = new FakeTimeProvider(T0);
        var (resolver, _, source) = NewResolver(assignments, time);
        var captain = NewPrincipal();

        await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        // First call hits LoadAssignmentsAsync once.
        await source.Received(1).LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());

        // Within the 60s TTL — no new load.
        time.Advance(TimeSpan.FromSeconds(30));
        await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);
        await source.Received(1).LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());

        // Past the TTL — reloads.
        time.Advance(TimeSpan.FromSeconds(35));
        await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);
        await source.Received(2).LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CapabilityGraph_DenialPropagates_AsNoMatchingRole()
    {
        // §2.1 step 6: capability graph denial returns NoMatchingRole.
        var graph = Substitute.For<ICapabilityGraph>();
        graph.QueryAsync(
            Arg.Any<PrincipalId>(), Arg.Any<Resource>(),
            Arg.Any<CapabilityAction>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));

        var (resolver, _, _) = NewResolver(WithCaptain(), capabilityGraph: graph);
        var captain = NewPrincipal();
        var resource = new Resource("res-1");

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Tactical, DeckDepth.MainDeck,
            ShipAction.Approve, resource);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.NoMatchingRole, denied.Reason);
    }

    [Fact]
    public async Task CapabilityGraph_GrantPlusProof_PopulatesGrantedProof()
    {
        // §2.1 step 8: when capability graph returns true and ExportProof
        // returns a proof, Granted.Proof is populated.
        var fakeProof = new CapabilityProof(
            Subject: PrincipalId.FromBytes(new byte[32]),
            Resource: new Resource("res-1"),
            Action: CapabilityAction.Write,
            OpChain: Array.Empty<SignedOperation<CapabilityOp>>(),
            ProvedAt: T0);
        var graph = Substitute.For<ICapabilityGraph>();
        graph.QueryAsync(
            Arg.Any<PrincipalId>(), Arg.Any<Resource>(),
            Arg.Any<CapabilityAction>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        graph.ExportProofAsync(
            Arg.Any<PrincipalId>(), Arg.Any<Resource>(),
            Arg.Any<CapabilityAction>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<CapabilityProof?>(fakeProof));

        var (resolver, _, _) = NewResolver(WithCaptain(), capabilityGraph: graph);
        var captain = NewPrincipal();
        var resource = new Resource("res-1");

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Tactical, DeckDepth.MainDeck,
            ShipAction.Approve, resource);

        var granted = Assert.IsType<PermissionDecision.Granted>(decision);
        Assert.Same(fakeProof, granted.Proof);
        Assert.Equal(ShipRole.Captain, granted.Role);
    }

    [Fact]
    public async Task MissionEnvelopeGate_Unavailable_DeniesWithUpgradeRemediation()
    {
        // §2.1 step 2: when the gate verdict is unavailable, the decision
        // is MissionEnvelopeUnavailable + UpgradeMissionEnvelope.
        var gate = Substitute.For<IShipActionMissionEnvelopeGate>();
        gate.EvaluateAsync(Arg.Any<ShipAction>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new MissionEnvelopeVerdict(
                IsAvailable: false,
                ReasonDisplay: "preview-only feature",
                RemediationDisplay: "Upgrade to the Pro edition.",
                CallToActionLabel: "Upgrade")));

        var (resolver, _, _) = NewResolver(WithCaptain(), envelopeGate: gate);
        var captain = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            captain, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.MissionEnvelopeUnavailable, denied.Reason);
        Assert.Equal(RemediationKind.UpgradeMissionEnvelope, denied.Remediation.Kind);
    }

    [Fact]
    public async Task ActionMinimumDeck_ContainsAllNineCanonicalActions()
    {
        // §2.1 step 0(a): every ShipAction has a minimum-deck entry.
        var keys = DefaultPermissionResolver.ActionMinimumDeck.Keys.ToList();
        Assert.Equal(9, keys.Count);
        Assert.Contains(ShipAction.Read, keys);
        Assert.Contains(ShipAction.Write, keys);
        Assert.Contains(ShipAction.IssueStandingOrder, keys);
        Assert.Contains(ShipAction.Approve, keys);
        Assert.Contains(ShipAction.PromoteRole, keys);
        Assert.Contains(ShipAction.StandWatch, keys);
        Assert.Contains(ShipAction.TransferWatch, keys);
        Assert.Contains(ShipAction.Quarantine, keys);
        Assert.Contains(ShipAction.OverrideQuarantine, keys);

        Assert.Equal(DeckDepth.BelowTheWaterline, DefaultPermissionResolver.ActionMinimumDeck[ShipAction.Quarantine]);
        Assert.Equal(DeckDepth.BelowTheWaterline, DefaultPermissionResolver.ActionMinimumDeck[ShipAction.OverrideQuarantine]);
    }

    [Fact]
    public void ShipRoleEnum_HasExactlyElevenValues()
    {
        // Per ADR 0077 §1: closed enum of 11 values.
        var values = Enum.GetValues<ShipRole>();
        Assert.Equal(11, values.Length);
    }

    [Fact]
    public async Task NullDependencies_ThrowsArgumentNullException()
    {
        var source = Substitute.For<IShipRoleAssignmentSource>();
        var audit = Substitute.For<IAuditTrail>();
        var signer = NewSigner();

        Assert.Throws<ArgumentNullException>(() => new DefaultPermissionResolver(
            assignmentSource: null!, auditTrail: audit, signer: signer,
            logger: NullLogger<DefaultPermissionResolver>.Instance));
        Assert.Throws<ArgumentNullException>(() => new DefaultPermissionResolver(
            assignmentSource: source, auditTrail: null!, signer: signer,
            logger: NullLogger<DefaultPermissionResolver>.Instance));
        Assert.Throws<ArgumentNullException>(() => new DefaultPermissionResolver(
            assignmentSource: source, auditTrail: audit, signer: null!,
            logger: NullLogger<DefaultPermissionResolver>.Instance));
        Assert.Throws<ArgumentNullException>(() => new DefaultPermissionResolver(
            assignmentSource: source, auditTrail: audit, signer: signer,
            logger: null!));
        await Task.CompletedTask;
    }

    // ===== Helpers =====

    private static IReadOnlyList<ShipRoleAssignment> WithCaptain()
        => WithRole(ShipRole.Captain);

    private static IReadOnlyList<ShipRoleAssignment> WithDivisionOfficer()
        => new[]
        {
            new ShipRoleAssignment(
                Tenant, ActorOf(NewPrincipalId()), ShipRole.DivisionOfficer,
                DivisionAssignment.DCA, T0, RotatesAt: null,
                IssuedBy: new StandingOrderId(Guid.NewGuid())),
        };

    private static IReadOnlyList<ShipRoleAssignment> WithRole(ShipRole role)
        => new[]
        {
            new ShipRoleAssignment(
                Tenant, ActorOf(NewPrincipalId()), role,
                Division: null, T0, RotatesAt: null,
                IssuedBy: new StandingOrderId(Guid.NewGuid())),
        };

    private static (DefaultPermissionResolver resolver, IAuditTrail audit, IShipRoleAssignmentSource source) NewResolver(
        IReadOnlyList<ShipRoleAssignment> assignments,
        FakeTimeProvider? time = null,
        ICapabilityGraph? capabilityGraph = null,
        IShipActionMissionEnvelopeGate? envelopeGate = null)
    {
        var source = Substitute.For<IShipRoleAssignmentSource>();
        // Tests pass principals but the resolver looks up assignments by
        // ActorId derived from PrincipalId.ToBase64Url(). Wire the source
        // to return the seeded assignments for ANY tenant (Phase 1 cache
        // is a coarse linear scan).
        source.LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(assignments));
        // ResolveAssignmentAsync (cold-path) returns the first assignment
        // (tests use a single-actor seed; the actor is whoever's principal
        // we're resolving, and the resolver derives their actor from
        // PrincipalId.ToBase64Url()).
        source.ResolveAssignmentAsync(Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var actor = ci.Arg<ActorId>();
                // Rebind the seeded assignment to the actor under test.
                return ValueTask.FromResult<ShipRoleAssignment?>(
                    assignments.Count == 0
                        ? null
                        : assignments[0] with { Holder = actor });
            });
        // Update LoadAssignmentsAsync to return the actor-bound assignment so the
        // cached lookup in FindAssignmentAsync hits.
        source.LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(assignments));

        var audit = Substitute.For<IAuditTrail>();
        var signer = NewSigner();
        var resolver = new DefaultPermissionResolver(
            source, audit, signer,
            NullLogger<DefaultPermissionResolver>.Instance,
            capabilityGraph: capabilityGraph,
            envelopeGate: envelopeGate,
            timeProvider: time ?? new FakeTimeProvider(T0));
        return (resolver, audit, source);
    }

    private static Principal NewPrincipal() => new Individual(NewPrincipalId());

    private static PrincipalId NewPrincipalId()
    {
        var bytes = new byte[32];
        Random.Shared.NextBytes(bytes);
        return PrincipalId.FromBytes(bytes);
    }

    private static ActorId ActorOf(PrincipalId pid) => new(pid.ToBase64Url());

    private static IOperationSigner NewSigner()
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

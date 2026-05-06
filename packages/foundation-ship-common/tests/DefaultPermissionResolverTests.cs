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
        var seed = WithCaptain();
        var (resolver, audit, _) = NewResolver(seed);
        var resource = new Resource("inv-1");

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Tactical, DeckDepth.MainDeck,
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
        var seed = WithDivisionOfficer();
        var (resolver, audit, _) = NewResolver(seed);
        var resource = new Resource("inv-1");

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Tactical, DeckDepth.MainDeck,
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
        Assert.Contains("elf-promotion", denied.ReasonDisplay); // case-tolerant substring
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
        Assert.Contains("nsufficient authority", denied.ReasonDisplay);
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
    public async Task ResourceScopeGuard_NullResourceForQuarantineDocument_Denied()
    {
        // W#50 P1 pre-merge security council 2026-05-06 (Major M1):
        // QuarantineDocument is resource-scoped — the §Trust-elevated
        // default closes the null-resource bypass at the resolver tier.
        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.EngineRoom, DeckDepth.MainDeck,
            ShipAction.QuarantineDocument, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied.Reason);
        Assert.Contains("esource-scoped", denied.ReasonDisplay);
    }

    [Fact]
    public async Task ResourceScopeGuard_NullResourceForApprove_Denied()
    {
        // §2.1 step 0(c): resource-scoped action requires non-null resource.
        var seed = WithCaptain();
        var (resolver, audit, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.MainDeck,
            ShipAction.Approve, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.SecurityPolicyBlocked, denied.Reason);
        Assert.Contains("esource-scoped", denied.ReasonDisplay);
    }

    [Fact]
    public async Task WatchPrecondition_StandWatch_ReturnsWatchRequired()
    {
        // §2.1 step 1: StandWatch requires OOD/EOOW designation. Phase 1
        // ships without IOnWatchProbe wired so all watch-required calls
        // return WatchRequired.
        var seed = WithCaptain();
        var (resolver, audit, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.MainDeck,
            ShipAction.StandWatch, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.WatchRequired, denied.Reason);
        Assert.Equal(RemediationKind.AwaitWatch, denied.Remediation.Kind);
    }

    [Fact]
    public async Task DeferralCheck_SupplyOffice_Phase2Deferred()
    {
        // §2.1 step 3: SupplyOffice short-circuits to Phase2Deferred.
        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.SupplyOffice, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.Phase2Deferred, denied.Reason);
        Assert.Equal(RemediationKind.Phase2Deferred, denied.Remediation.Kind);
    }

    [Fact]
    public async Task DeferralCheck_Wardroom_V2Deferred()
    {
        // §2.1 step 3: Wardroom short-circuits to V2Deferred.
        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Wardroom, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.V2Deferred, denied.Reason);
    }

    [Fact]
    public async Task RoleMatch_NoAssignment_Denied_NoMatchingRole()
    {
        // §2.1 step 4: subject without an assignment in the asserted
        // tenant gets NoMatchingRole.
        var (resolver, _, _) = NewResolver(assignments: Array.Empty<ShipRoleAssignment>());
        var stranger = NewPrincipal();

        var decision = await resolver.ResolveAsync(
            Tenant, stranger, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.NoMatchingRole, denied.Reason);
    }

    [Fact]
    public async Task CacheStampede_ConcurrentColdLoads_HitSourceExactlyOnce()
    {
        // W#46 P1 pre-merge security council 2026-05-06: regression test
        // for cache stampede. When N callers simultaneously observe an
        // expired TTL, only ONE upstream load happens; the rest await the
        // same in-flight Task.
        var seed = WithCaptain();
        var slowSource = Substitute.For<IShipRoleAssignmentSource>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        slowSource.LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<IReadOnlyList<ShipRoleAssignment>>(
                release.Task.ContinueWith(_ => seed.Assignments)));
        var resolver = new DefaultPermissionResolver(
            slowSource, Substitute.For<IAuditTrail>(), NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            timeProvider: new FakeTimeProvider(T0));

        // Kick off 10 concurrent ResolveAsync calls; all observe the
        // empty cache and try to load.
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            resolver.ResolveAsync(
                Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
                ShipAction.Read, resource: null).AsTask()).ToArray();

        // Allow the in-flight load to complete.
        release.SetResult(true);
        await Task.WhenAll(tasks);

        // Source should have been hit exactly ONCE despite 10 concurrent
        // callers.
        await slowSource.Received(1).LoadAssignmentsAsync(
            Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantBinding_LookupRespectsCallerAssertedTenant()
    {
        // §Trust (W#46 P1 pre-merge security council 2026-05-06):
        // ResolveAsync uses the caller-asserted TenantId for assignment
        // lookups. A principal with no assignment in tenant-B receives
        // NoMatchingRole even when the source returns assignments for
        // tenant-A — there is no cross-tenant authority bleed.
        var actorId = NewPrincipalId();
        var captainInTenantA = new[]
        {
            new ShipRoleAssignment(
                Tenant, ActorOf(actorId), ShipRole.Captain,
                Division: null, T0, RotatesAt: null,
                IssuedBy: new StandingOrderId(Guid.NewGuid())),
        };
        var source = Substitute.For<IShipRoleAssignmentSource>();
        source.LoadAssignmentsAsync(Tenant, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(captainInTenantA));
        // tenant-B has no assignments for this actor.
        source.LoadAssignmentsAsync(new TenantId("tenant-b"), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(Array.Empty<ShipRoleAssignment>()));
        var audit = Substitute.For<IAuditTrail>();
        var resolver = new DefaultPermissionResolver(
            source, audit, NewSigner(),
            NullLogger<DefaultPermissionResolver>.Instance,
            timeProvider: new FakeTimeProvider(T0));
        var subject = new Individual(actorId);

        // Resolve in tenant-B → should be NoMatchingRole, NOT Granted-as-Captain.
        var decision = await resolver.ResolveAsync(
            new TenantId("tenant-b"), subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.NoMatchingRole, denied.Reason);
    }

    [Fact]
    public async Task RoleMatch_SUPPO_Phase2Deferred_PerSection16()
    {
        // §1.6: SUPPO is structurally valid but operationally inert.
        var seed = WithRole(ShipRole.SUPPO);
        var (resolver, _, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.Phase2Deferred, denied.Reason);
    }

    [Fact]
    public async Task LocationScope_DivisionOfficerInSickBay_LocationOutOfScope()
    {
        // Division Officer is excluded from SickBay (medical specialist
        // territory).
        var seed = WithDivisionOfficer();
        var (resolver, _, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.SickBay, DeckDepth.MainDeck,
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
            Tenant, stranger, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
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
        var seed = WithDivisionOfficer();
        var (resolver, audit, _) = NewResolver(seed);

        await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.SickBay, DeckDepth.MainDeck,
            ShipAction.Read, resource: null);

        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType == AuditEventType.PermissionDenied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GrantedDecision_DoesNotEmit_PermissionDenied()
    {
        // §2.4: Granted decisions are NOT audit-loud by default in Phase 1.
        var seed = WithCaptain();
        var (resolver, audit, _) = NewResolver(seed);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
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
        var seed = WithDivisionOfficer();
        var (resolver, audit, _) = NewResolver(seed);

        // Call 11 times (same actor + location): each Denied(LocationOutOfScope).
        for (var i = 0; i < 11; i++)
        {
            await resolver.ResolveAsync(
                Tenant, seed.Subject, ShipLocation.SickBay, DeckDepth.MainDeck,
                ShipAction.Read, resource: null);
        }

        // 12th call: short-circuit — counter (11) > limit (10).
        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.SickBay, DeckDepth.MainDeck,
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
        var seed = WithCaptain();
        var time = new FakeTimeProvider(T0);
        var (resolver, _, source) = NewResolver(seed, time);

        await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        // First call hits LoadAssignmentsAsync once.
        await source.Received(1).LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());

        // Within the 60s TTL — no new load.
        time.Advance(TimeSpan.FromSeconds(30));
        await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);
        await source.Received(1).LoadAssignmentsAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());

        // Past the TTL — reloads.
        time.Advance(TimeSpan.FromSeconds(35));
        await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
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

        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed, capabilityGraph: graph);
        var resource = new Resource("res-1");

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Tactical, DeckDepth.MainDeck,
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

        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed, capabilityGraph: graph);
        var resource = new Resource("res-1");

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Tactical, DeckDepth.MainDeck,
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

        var seed = WithCaptain();
        var (resolver, _, _) = NewResolver(seed, envelopeGate: gate);

        var decision = await resolver.ResolveAsync(
            Tenant, seed.Subject, ShipLocation.Quarterdeck, DeckDepth.TopDeck,
            ShipAction.Read, resource: null);

        var denied = Assert.IsType<PermissionDecision.Denied>(decision);
        Assert.Equal(DenialReason.MissionEnvelopeUnavailable, denied.Reason);
        Assert.Equal(RemediationKind.UpgradeMissionEnvelope, denied.Remediation.Kind);
    }

    [Fact]
    public async Task ActionMinimumDeck_ContainsAllCanonicalActions()
    {
        // §2.1 step 0(a): every ShipAction has a minimum-deck entry.
        // Cardinality is the 9 ADR 0077 §2 canonical actions PLUS any
        // cohort-extension actions added by downstream W#35 ADRs (e.g.,
        // ADR 0083 §4 added the 4 Ship's Office actions in W#55 P1).
        var keys = DefaultPermissionResolver.ActionMinimumDeck.Keys.ToList();
        Assert.Contains(ShipAction.Read, keys);
        Assert.Contains(ShipAction.Write, keys);
        Assert.Contains(ShipAction.IssueStandingOrder, keys);
        Assert.Contains(ShipAction.Approve, keys);
        Assert.Contains(ShipAction.PromoteRole, keys);
        Assert.Contains(ShipAction.StandWatch, keys);
        Assert.Contains(ShipAction.TransferWatch, keys);
        Assert.Contains(ShipAction.Quarantine, keys);
        Assert.Contains(ShipAction.OverrideQuarantine, keys);
        // W#55 Ship's Office cohort additions.
        Assert.Contains(ShipAction.ViewShipsOffice, keys);
        Assert.Contains(ShipAction.EditShipsOfficeDocument, keys);
        Assert.Contains(ShipAction.PublishShipsOfficeDocument, keys);
        Assert.Contains(ShipAction.ArchiveShipsOfficeDocument, keys);
        // W#50 Engine Room cohort additions.
        Assert.Contains(ShipAction.ViewEngineRoom, keys);
        Assert.Contains(ShipAction.ViewDamageControl, keys);
        Assert.Contains(ShipAction.QuarantineDocument, keys);
        Assert.Contains(ShipAction.ReleaseQuarantine, keys);
        Assert.Contains(ShipAction.CompactDocument, keys);
        // W#54 Sick Bay cohort additions.
        Assert.Contains(ShipAction.ViewSickBay, keys);
        Assert.Contains(ShipAction.ViewPharmacy, keys);
        Assert.Contains(ShipAction.ManageRecoveryContacts, keys);
        Assert.Contains(ShipAction.TriggerKeyRotation, keys);
        Assert.Contains(ShipAction.InitiateMedevac, keys);
        Assert.Contains(ShipAction.AuthorizeMedevac, keys);
        Assert.Contains(ShipAction.ViewFirstAid, keys);

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

    private record struct RoleSeed(Principal Subject, IReadOnlyList<ShipRoleAssignment> Assignments);

    private static RoleSeed WithCaptain() => WithRole(ShipRole.Captain);

    private static RoleSeed WithDivisionOfficer()
    {
        var pid = NewPrincipalId();
        var assignment = new ShipRoleAssignment(
            Tenant, ActorOf(pid), ShipRole.DivisionOfficer,
            DivisionAssignment.DCA, T0, RotatesAt: null,
            IssuedBy: new StandingOrderId(Guid.NewGuid()));
        return new RoleSeed(new Individual(pid), new[] { assignment });
    }

    private static RoleSeed WithRole(ShipRole role)
    {
        var pid = NewPrincipalId();
        var assignment = new ShipRoleAssignment(
            Tenant, ActorOf(pid), role,
            Division: null, T0, RotatesAt: null,
            IssuedBy: new StandingOrderId(Guid.NewGuid()));
        return new RoleSeed(new Individual(pid), new[] { assignment });
    }

    /// <summary>
    /// Test convenience: build a resolver whose source returns
    /// <paramref name="assignments"/>. The caller pairs each test's
    /// resolved subject with a matching assignment via <see cref="WithRole"/>
    /// / <see cref="WithCaptain"/> / <see cref="WithDivisionOfficer"/> to
    /// keep the assignment's <see cref="ShipRoleAssignment.Holder"/>
    /// aligned with the principal under test.
    /// </summary>
    private static (DefaultPermissionResolver resolver, IAuditTrail audit, IShipRoleAssignmentSource source) NewResolver(
        IReadOnlyList<ShipRoleAssignment> assignments,
        FakeTimeProvider? time = null,
        ICapabilityGraph? capabilityGraph = null,
        IShipActionMissionEnvelopeGate? envelopeGate = null)
    {
        var source = Substitute.For<IShipRoleAssignmentSource>();
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

    private static (DefaultPermissionResolver resolver, IAuditTrail audit, IShipRoleAssignmentSource source) NewResolver(
        RoleSeed seed,
        FakeTimeProvider? time = null,
        ICapabilityGraph? capabilityGraph = null,
        IShipActionMissionEnvelopeGate? envelopeGate = null)
        => NewResolver(seed.Assignments, time, capabilityGraph, envelopeGate);

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

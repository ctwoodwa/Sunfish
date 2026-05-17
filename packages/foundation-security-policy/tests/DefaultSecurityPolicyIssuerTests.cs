using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.SecurityPolicy.Issuance;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.SecurityPolicy.Validation;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;
using CapabilityProof = Sunfish.Foundation.SecurityPolicy.Issuance.CapabilityProof;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

/// <summary>
/// W#37 P1 PR 3b.1 — issuer-flow coverage for <see cref="DefaultSecurityPolicyIssuer"/>.
/// Pure-logic floor tests live in <see cref="IssuanceTests"/>; these
/// tests exercise the end-to-end ProposeAsync / ApproveAsync /
/// RescindAsync pipeline with stubbed collaborators.
/// </summary>
public sealed class DefaultSecurityPolicyIssuerTests
{
    private static readonly TenantId Tenant = new("tenant-pr3b1");
    private static readonly ActorId Proposer = new("actor-proposer");
    private static readonly ActorId CaptainActor = new("actor-captain");
    private static readonly ActorId OfficerActor = new("actor-officer");
    private static readonly DateTimeOffset Now = new(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);

    private static TenantSecurityPolicy DefaultPolicy()
        => TenantSecurityPolicy.DefaultFor(Tenant, Now);

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class FakeSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);
        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
            => ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    private sealed class StubRoleSource : IShipRoleAssignmentSource
    {
        public List<ShipRoleAssignment> Assignments { get; } = new();
        public ValueTask<IReadOnlyList<ShipRoleAssignment>> LoadAssignmentsAsync(TenantId tenantId, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<ShipRoleAssignment>>(Assignments);
    }

    private sealed class StubPrincipalResolver : IActorPrincipalResolver
    {
        public Dictionary<ActorId, Principal?> Map { get; } = new();
        public ValueTask<Principal?> ResolveAsync(TenantId tenant, ActorId actor, CancellationToken ct = default)
            => ValueTask.FromResult(Map.TryGetValue(actor, out var p) ? p : null);
    }

    private sealed class StubStandingOrderIssuer : IStandingOrderIssuer
    {
        public List<StandingOrderDraft> Issued { get; } = new();

        public Task<StandingOrder> IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)
        {
            Issued.Add(draft);
            var order = new StandingOrder(
                Id: new StandingOrderId(Guid.NewGuid()),
                TenantId: draft.TenantId,
                IssuedBy: issuedBy,
                IssuedAt: DateTimeOffset.UtcNow,
                Scope: draft.Scope,
                Triples: draft.Triples,
                Rationale: draft.Rationale,
                ApprovalChain: draft.ApprovalChain,
                AuditRecordId: new AuditRecordId(Guid.NewGuid()),
                State: StandingOrderState.Validated);
            return Task.FromResult(order);
        }

        public Task<StandingOrder> RescindAsync(StandingOrderId id, ActorId rescindedBy, string rationale, IAuditTrail auditTrail, CancellationToken ct)
            => Task.FromResult(new StandingOrder(
                Id: id,
                TenantId: Tenant,
                IssuedBy: rescindedBy,
                IssuedAt: DateTimeOffset.UtcNow,
                Scope: StandingOrderScope.Security,
                Triples: Array.Empty<StandingOrderTriple>(),
                Rationale: rationale,
                ApprovalChain: null,
                AuditRecordId: new AuditRecordId(Guid.NewGuid()),
                State: StandingOrderState.Rescinded));
    }

    private sealed class HappyValidator : ISecurityPolicyValidator
    {
        public SecurityPolicyValidatorPriority Priority => SecurityPolicyValidatorPriority.Schema;
        public ValueTask<SecurityPolicyValidationResult> ValidateAsync(TenantSecurityPolicy proposed, TenantSecurityPolicy current, SecurityPolicyValidationContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new SecurityPolicyValidationResult(Array.Empty<SecurityPolicyValidationFinding>()));
    }

    private sealed class ErrorValidator : ISecurityPolicyValidator
    {
        public SecurityPolicyValidatorPriority Priority => SecurityPolicyValidatorPriority.Schema;
        public ValueTask<SecurityPolicyValidationResult> ValidateAsync(TenantSecurityPolicy proposed, TenantSecurityPolicy current, SecurityPolicyValidationContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new SecurityPolicyValidationResult(new[]
            {
                SecurityPolicyValidationFinding.Error("TEST_ERROR", "synthetic error for test", "fix the input"),
            }));
    }

    private static ShipRoleAssignment Assignment(ActorId actor, ShipRole role)
        => new(Tenant, actor, role, null, Now.AddDays(-1), null, new StandingOrderId(Guid.NewGuid()));

    private static (DefaultSecurityPolicyIssuer Issuer, StubStandingOrderIssuer SO, IAuditTrail Audit, StubRoleSource Roles, StubPrincipalResolver Principals, FakeTimeProvider Time)
        Build(IEnumerable<ISecurityPolicyValidator>? validators = null)
    {
        var so = new StubStandingOrderIssuer();
        var audit = Substitute.For<IAuditTrail>();
        var roles = new StubRoleSource();
        var principals = new StubPrincipalResolver();
        var time = new FakeTimeProvider();
        var issuer = new DefaultSecurityPolicyIssuer(
            validators: validators ?? new[] { new HappyValidator() },
            standingOrderIssuer: so,
            approvalFloor: new DefaultSecurityPolicyApprovalFloorProvider(),
            principalResolver: principals,
            roleSource: roles,
            auditTrail: audit,
            signer: new FakeSigner(),
            time: time,
            policyLoader: (_, _) => ValueTask.FromResult(DefaultPolicy()),
            options: Options.Create(new SecurityPolicyIssuerOptions()));
        return (issuer, so, audit, roles, principals, time);
    }

    private static CapabilityProof FreshProofFor(ActorId who, StandingOrderId proposal)
        => new(who, proposal, IssuedAt: Now, ExpiresAt: Now.AddHours(1), ProofBytes: new byte[] { 0xAB });

    [Fact]
    public async Task Issuer_Propose_EmitsSecurityPolicyProposed_Audit()
    {
        var (issuer, _, audit, roles, _, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        roles.Assignments.Add(Assignment(Proposer, ShipRole.EngineerOfficer));

        var id = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test propose", CancellationToken.None);

        Assert.NotEqual(default, id);
        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyProposed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Propose_RejectsOnValidatorError_AndEmitsRejectedAudit()
    {
        var (issuer, _, audit, roles, _, _) = Build(new ISecurityPolicyValidator[] { new ErrorValidator() });
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));

        await Assert.ThrowsAsync<SecurityPolicyRejectedException>(() =>
            issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None).AsTask());

        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyRejected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Approve_TransitionsToApplied_WhenFloorSatisfied()
    {
        var (issuer, _, audit, roles, principals, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        roles.Assignments.Add(Assignment(OfficerActor, ShipRole.EngineerOfficer));
        roles.Assignments.Add(Assignment(Proposer, ShipRole.EngineerOfficer));
        principals.Map[CaptainActor] = new Individual(PrincipalId.FromBytes(new byte[32]));
        principals.Map[OfficerActor] = new Individual(PrincipalId.FromBytes(new byte[32]));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        var r1 = await issuer.ApproveAsync(Tenant, CaptainActor, proposalId, FreshProofFor(CaptainActor, proposalId), comment: null, CancellationToken.None);
        Assert.False(r1.IsApprovalChainSatisfied);
        Assert.Equal(1, r1.ApprovalsGranted);

        var r2 = await issuer.ApproveAsync(Tenant, OfficerActor, proposalId, FreshProofFor(OfficerActor, proposalId), comment: null, CancellationToken.None);
        Assert.True(r2.IsApprovalChainSatisfied);
        Assert.Equal(2, r2.ApprovalsGranted);

        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyApplied)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Approve_RejectsStaleProof()
    {
        var (issuer, _, audit, roles, principals, time) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        principals.Map[CaptainActor] = new Individual(PrincipalId.FromBytes(new byte[32]));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        // Mint a proof that expires NOW, then advance the clock so it's stale.
        var staleProof = new CapabilityProof(CaptainActor, proposalId, Now.AddHours(-2), Now, new byte[] { 0xCC });
        time.Current = Now.AddSeconds(1);

        await Assert.ThrowsAsync<SecurityPolicyRejectedException>(() =>
            issuer.ApproveAsync(Tenant, CaptainActor, proposalId, staleProof, comment: null, CancellationToken.None).AsTask());

        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyRejected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Approve_RejectsMismatchedBinding()
    {
        var (issuer, _, _, roles, principals, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        principals.Map[CaptainActor] = new Individual(PrincipalId.FromBytes(new byte[32]));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        // Proof bound to a DIFFERENT proposal id.
        var otherProposal = new StandingOrderId(Guid.NewGuid());
        var mismatchedProof = FreshProofFor(CaptainActor, otherProposal);

        await Assert.ThrowsAsync<SecurityPolicyRejectedException>(() =>
            issuer.ApproveAsync(Tenant, CaptainActor, proposalId, mismatchedProof, comment: null, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Issuer_PrincipalResolverReturnsNull_FailsClosed()
    {
        var (issuer, _, audit, roles, _, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        // principals.Map[CaptainActor] intentionally NOT set → resolver returns null.

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        await Assert.ThrowsAsync<SecurityPolicyRejectedException>(() =>
            issuer.ApproveAsync(Tenant, CaptainActor, proposalId, FreshProofFor(CaptainActor, proposalId), comment: null, CancellationToken.None).AsTask());

        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyRejected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Rescind_NonProposerNonCaptain_Throws()
    {
        var (issuer, _, _, roles, _, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));
        roles.Assignments.Add(Assignment(OfficerActor, ShipRole.EngineerOfficer));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        // OfficerActor is neither the proposer nor a Captain — rescission denied.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            issuer.RescindAsync(Tenant, OfficerActor, proposalId, "test", CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Issuer_Approve_Concurrent_DoesNotLoseApprovals()
    {
        // Council .NET-architect A.1 regression guard: lost-update race on
        // _inFlight could let two concurrent ApproveAsync calls observe the
        // same baseline + later writer clobbers earlier approval. AddOrUpdate
        // CAS prevents this. Verify by racing 3 distinct approvers (floor is
        // 2 so first 2 to land transition Applied; third sees PROPOSAL_NOT_FOUND
        // because _inFlight was removed). Without CAS, an approver could be
        // silently dropped — this test would intermittently see ApprovalsGranted
        // counts like {1,1,…} instead of {1,2,…}.
        var (issuer, _, audit, roles, principals, _) = Build();
        var a1 = new ActorId("actor-a1");
        var a2 = new ActorId("actor-a2");
        var a3 = new ActorId("actor-a3");
        roles.Assignments.Add(Assignment(a1, ShipRole.Captain));
        roles.Assignments.Add(Assignment(a2, ShipRole.EngineerOfficer));
        roles.Assignments.Add(Assignment(a3, ShipRole.EngineerOfficer));
        roles.Assignments.Add(Assignment(Proposer, ShipRole.EngineerOfficer));
        principals.Map[a1] = new Individual(PrincipalId.FromBytes(new byte[32]));
        principals.Map[a2] = new Individual(PrincipalId.FromBytes(new byte[32]));
        principals.Map[a3] = new Individual(PrincipalId.FromBytes(new byte[32]));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        var tasks = new[] { a1, a2, a3 }
            .Select(approver => Task.Run(async () =>
            {
                try
                {
                    return (Success: true, Result: await issuer.ApproveAsync(
                        Tenant, approver, proposalId,
                        FreshProofFor(approver, proposalId), comment: null, CancellationToken.None));
                }
                catch (InvalidOperationException)
                {
                    // Expected for the racing-loser whose proposal was removed
                    // after Applied transition by an earlier approver.
                    return (Success: false, Result: default(SecurityPolicyApprovalResult)!);
                }
            }))
            .ToArray();

        var outcomes = await Task.WhenAll(tasks);
        var successes = outcomes.Where(o => o.Success).Select(o => o.Result).ToArray();

        // At least 2 approvers must have landed (the minimum floor); the third
        // may either land + return Applied=true, or race-lose + throw
        // InvalidOperationException with PROPOSAL_NOT_FOUND audit.
        Assert.True(successes.Length >= 2, "At least 2 of the 3 approvers should have landed on the in-flight proposal.");

        // The CAS append guarantees ApprovalsGranted values are strictly
        // increasing 1..N (no duplicates) — proves no lost update.
        var granted = successes.Select(r => r.ApprovalsGranted).OrderBy(n => n).ToArray();
        Assert.Equal(granted.Distinct().Count(), granted.Length);

        // Apply-once race-guard: exactly ONE SecurityPolicyApplied audit
        // must have been emitted, even when multiple approvers concurrently
        // observe the floor as satisfied. (Multiple successes may report
        // IsApprovalChainSatisfied=true factually — only one performs the
        // underlying StandingOrder transition + audit.)
        await audit.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyApplied)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Issuer_Rescind_Proposer_Succeeds()
    {
        var (issuer, _, audit, roles, _, _) = Build();
        roles.Assignments.Add(Assignment(CaptainActor, ShipRole.Captain));

        var proposalId = await issuer.ProposeAsync(Tenant, Proposer, DefaultPolicy(), "test", CancellationToken.None);

        await issuer.RescindAsync(Tenant, Proposer, proposalId, "no longer needed", CancellationToken.None);

        await audit.Received().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.SecurityPolicyRescinded)),
            Arg.Any<CancellationToken>());
    }
}

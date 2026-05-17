using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Issuance;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Xunit;

namespace Sunfish.Foundation.SecurityPolicy.Tests;

/// <summary>
/// W#37 P1 PR 3b.1 — coverage for §3.1 ApprovalChain floor invariants on
/// <see cref="DefaultSecurityPolicyApprovalFloorProvider"/>. The issuer
/// itself (<c>DefaultSecurityPolicyIssuer</c>) lands in PR 3b.1 impl;
/// these tests pin the pure-logic floor behavior independently.
/// </summary>
public sealed class IssuanceTests
{
    private static readonly StandingOrderId Proposal = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly ActorId Proposer = new("actor-proposer");
    private static readonly ActorId Captain = new("actor-captain");
    private static readonly ActorId Xo = new("actor-xo");
    private static readonly ActorId Officer = new("actor-officer");
    private static readonly DateTimeOffset Now = new(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);

    private static ApprovalStep Step(ActorId who, int hoursAgo = 0)
        => new(Approver: who, ApprovedAt: Now.AddHours(-hoursAgo), Comment: null);

    private static IReadOnlyDictionary<ActorId, DateTimeOffset> FreshProofsFor(params ActorId[] approvers)
    {
        var dict = new Dictionary<ActorId, DateTimeOffset>();
        foreach (var a in approvers) dict[a] = Now.AddHours(1);
        return dict;
    }

    private readonly DefaultSecurityPolicyApprovalFloorProvider _floor = new();

    [Fact]
    public void ApprovalFloorProvider_InsufficientApprovers_Blocks()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Captain) }),
            approverRoles: new Dictionary<ActorId, ShipRole> { [Captain] = ShipRole.Captain },
            proofExpiriesByApprover: FreshProofsFor(Captain),
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_INSUFFICIENT_APPROVERS", verdict.BlockReasonCode);
        Assert.False(string.IsNullOrEmpty(verdict.BlockReasonAccessibleMessage));
    }

    [Fact]
    public void ApprovalFloorProvider_NoSeniorApprover_Blocks()
    {
        var second = new ActorId("actor-officer-2");
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Officer), Step(second) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Officer] = ShipRole.EngineerOfficer,
                [second]  = ShipRole.EngineerOfficer,
            },
            proofExpiriesByApprover: FreshProofsFor(Officer, second),
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_NO_SENIOR_APPROVER", verdict.BlockReasonCode);
    }

    [Fact]
    public void ApprovalFloorProvider_SelfApproval_Blocks()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Proposer), Step(Captain) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Proposer] = ShipRole.EngineerOfficer,
                [Captain]  = ShipRole.Captain,
            },
            proofExpiriesByApprover: FreshProofsFor(Proposer, Captain),
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_SELF_APPROVAL", verdict.BlockReasonCode);
    }

    [Fact]
    public void ApprovalFloorProvider_DuplicateApprovers_Blocks()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Captain), Step(Captain, hoursAgo: 1) }),
            approverRoles: new Dictionary<ActorId, ShipRole> { [Captain] = ShipRole.Captain },
            proofExpiriesByApprover: FreshProofsFor(Captain),
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_DUPLICATE_APPROVERS", verdict.BlockReasonCode);
    }

    [Fact]
    public void ApprovalFloorProvider_ExpiredProof_Blocks()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Captain), Step(Officer) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Captain] = ShipRole.Captain,
                [Officer] = ShipRole.EngineerOfficer,
            },
            // Captain proof expired 1h ago; Officer proof still fresh.
            proofExpiriesByApprover: new Dictionary<ActorId, DateTimeOffset>
            {
                [Captain] = Now.AddHours(-1),
                [Officer] = Now.AddHours(1),
            },
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_EXPIRED_PROOF", verdict.BlockReasonCode);
        Assert.Contains("actor-captain", verdict.BlockReasonAccessibleMessage);
    }

    [Fact]
    public void ApprovalFloorProvider_MissingProofExpiry_Blocks()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Captain), Step(Officer) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Captain] = ShipRole.Captain,
                [Officer] = ShipRole.EngineerOfficer,
            },
            // Officer has no recorded expiry — fail-closed.
            proofExpiriesByApprover: new Dictionary<ActorId, DateTimeOffset>
            {
                [Captain] = Now.AddHours(1),
            },
            now: Now);

        Assert.False(verdict.AllowApply);
        Assert.Equal("FLOOR_EXPIRED_PROOF", verdict.BlockReasonCode);
    }

    [Fact]
    public void ApprovalFloorProvider_CaptainPlusOfficer_Allows()
    {
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Captain), Step(Officer) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Captain] = ShipRole.Captain,
                [Officer] = ShipRole.EngineerOfficer,
            },
            proofExpiriesByApprover: FreshProofsFor(Captain, Officer),
            now: Now);

        Assert.True(verdict.AllowApply);
        Assert.Null(verdict.BlockReasonCode);
        Assert.Null(verdict.BlockReasonAccessibleMessage);
    }

    [Fact]
    public void ApprovalFloorProvider_XoSatisfiesSenior_Allows()
    {
        // §3.2 Captain-vacancy — XO substitutes for the senior approver slot.
        var verdict = _floor.Evaluate(
            proposal: Proposal,
            proposer: Proposer,
            chainSoFar: new ApprovalChain(new[] { Step(Xo), Step(Officer) }),
            approverRoles: new Dictionary<ActorId, ShipRole>
            {
                [Xo]      = ShipRole.XO,
                [Officer] = ShipRole.EngineerOfficer,
            },
            proofExpiriesByApprover: FreshProofsFor(Xo, Officer),
            now: Now);

        Assert.True(verdict.AllowApply);
        Assert.Null(verdict.BlockReasonCode);
    }

    [Fact]
    public void CapabilityProof_IsFresh_ReturnsTrueWhenExpiryInFuture()
    {
        var proof = new CapabilityProof(
            Approver: Captain,
            BoundTo: Proposal,
            IssuedAt: Now,
            ExpiresAt: Now.AddHours(1),
            ProofBytes: new byte[] { 0x01 });

        Assert.True(proof.IsFresh(Now));
        Assert.False(proof.IsFresh(Now.AddHours(2)));
    }

    [Fact]
    public void CapabilityProof_IsBoundTo_ReturnsTrueOnlyForMatchingProposal()
    {
        var proof = new CapabilityProof(
            Approver: Captain,
            BoundTo: Proposal,
            IssuedAt: Now,
            ExpiresAt: Now.AddHours(1),
            ProofBytes: new byte[] { 0x01 });

        Assert.True(proof.IsBoundTo(Proposal));
        Assert.False(proof.IsBoundTo(new StandingOrderId(Guid.Parse("22222222-2222-2222-2222-222222222222"))));
    }
}

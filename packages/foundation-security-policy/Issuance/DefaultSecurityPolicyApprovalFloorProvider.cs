using System.Collections.Generic;
using System.Linq;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Canonical 5-invariant implementation of
/// <see cref="ISecurityPolicyApprovalFloorProvider"/>. Per ADR 0068
/// §3.1.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Invariants evaluated in deterministic order; the FIRST failing
/// invariant wins (the verdict is the most-specific available
/// diagnosis). Order matters for audit forensics — when a future
/// invariant is added it MUST be appended to the bottom, never
/// inserted, so historical audit reason codes remain stable.
/// </para>
/// <para>
/// <b>Evaluation order is 1, 3, 4, 2, 5</b> — see invariant-specific
/// comments at each branch in <see cref="Evaluate"/>. Spec §3.1 lists
/// the invariants 1→5 in narrative order; the impl reorders so a
/// self-approval or duplicate-approver structural defect surfaces
/// BEFORE "no senior approver" (which would otherwise mask the more
/// specific diagnosis under contention). All five invariants still
/// run on every call; only the FIRST failure short-circuits.
/// </para>
/// <para>
/// §3.2 Captain-vacancy: when <see cref="ShipRole.Captain"/> is absent
/// from the approver-roles snapshot the senior-approver invariant
/// (#2) is satisfied by an <see cref="ShipRole.XO"/> approver. The
/// XO-elevation is recorded by the issuer in the
/// <c>SecurityPolicyProposed</c> audit payload's
/// <c>CaptainVacancyExceptionInvoked</c> flag; this provider does
/// NOT emit the audit event itself.
/// </para>
/// </remarks>
public sealed class DefaultSecurityPolicyApprovalFloorProvider : ISecurityPolicyApprovalFloorProvider
{
    /// <summary>Minimum approver count required by §3.1 invariant #1.</summary>
    public const int MinimumApproverCount = 2;

    /// <inheritdoc />
    public ApprovalFloorVerdict Evaluate(
        StandingOrderId proposal,
        ActorId proposer,
        ApprovalChain chainSoFar,
        IReadOnlyDictionary<ActorId, ShipRole> approverRoles,
        IReadOnlyDictionary<ActorId, System.DateTimeOffset> proofExpiriesByApprover,
        System.DateTimeOffset now)
    {
        if (chainSoFar is null) throw new System.ArgumentNullException(nameof(chainSoFar));
        if (approverRoles is null) throw new System.ArgumentNullException(nameof(approverRoles));
        if (proofExpiriesByApprover is null) throw new System.ArgumentNullException(nameof(proofExpiriesByApprover));

        var steps = chainSoFar.Steps;

        // Invariant 1: at least MinimumApproverCount approvers in the chain.
        if (steps.Count < MinimumApproverCount)
        {
            return new ApprovalFloorVerdict(
                AllowApply: false,
                BlockReasonCode: "FLOOR_INSUFFICIENT_APPROVERS",
                BlockReasonAccessibleMessage:
                    $"Security-policy changes require at least {MinimumApproverCount} approvers; {steps.Count} recorded so far.");
        }

        // Invariant 3: proposer must not appear in the approval chain.
        // (Evaluated before #2 because self-approval is a higher-severity diagnosis
        // than "no senior approver" — if the proposer self-approved we want the
        // audit log to record THAT rather than a downstream symptom.)
        if (steps.Any(s => s.Approver == proposer))
        {
            return new ApprovalFloorVerdict(
                AllowApply: false,
                BlockReasonCode: "FLOOR_SELF_APPROVAL",
                BlockReasonAccessibleMessage:
                    "The proposer of a security-policy change may not also approve it.");
        }

        // Invariant 4: all approver ActorIds in the chain must be distinct.
        // (Evaluated before #2 for the same reason — duplicate approvers is a
        // structural defect we want surfaced explicitly.)
        var distinctApprovers = new HashSet<ActorId>();
        foreach (var step in steps)
        {
            if (!distinctApprovers.Add(step.Approver))
            {
                return new ApprovalFloorVerdict(
                    AllowApply: false,
                    BlockReasonCode: "FLOOR_DUPLICATE_APPROVERS",
                    BlockReasonAccessibleMessage:
                        $"Approver '{step.Approver.Value}' appears more than once in the approval chain.");
            }
        }

        // Invariant 2: at least one approver holds Captain OR XO (§3.2 Captain-vacancy
        // allows XO to satisfy the senior-approver slot).
        var hasSenior = steps.Any(s =>
            approverRoles.TryGetValue(s.Approver, out var role)
            && (role == ShipRole.Captain || role == ShipRole.XO));
        if (!hasSenior)
        {
            return new ApprovalFloorVerdict(
                AllowApply: false,
                BlockReasonCode: "FLOOR_NO_SENIOR_APPROVER",
                BlockReasonAccessibleMessage:
                    "At least one approver must hold the Captain or XO role.");
        }

        // Invariant 5: every approver's CapabilityProof.ExpiresAt > now.
        foreach (var step in steps)
        {
            if (!proofExpiriesByApprover.TryGetValue(step.Approver, out var expiresAt))
            {
                // Defensive — if the issuer wires this provider correctly, every
                // approver in the chain has a recorded proof expiry. Surface as
                // FLOOR_EXPIRED_PROOF for forensic clarity rather than a silent
                // pass.
                return new ApprovalFloorVerdict(
                    AllowApply: false,
                    BlockReasonCode: "FLOOR_EXPIRED_PROOF",
                    BlockReasonAccessibleMessage:
                        $"Approver '{step.Approver.Value}' has no recorded capability-proof expiry.");
            }
            if (expiresAt <= now)
            {
                return new ApprovalFloorVerdict(
                    AllowApply: false,
                    BlockReasonCode: "FLOOR_EXPIRED_PROOF",
                    BlockReasonAccessibleMessage:
                        $"Approver '{step.Approver.Value}' capability proof expired at {expiresAt:O}; current time is {now:O}.");
            }
        }

        return new ApprovalFloorVerdict(AllowApply: true, BlockReasonCode: null, BlockReasonAccessibleMessage: null);
    }
}

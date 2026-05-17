namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Result returned by
/// <see cref="ISecurityPolicyIssuer.ApproveAsync"/>. Per ADR 0068 §3.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068. When
/// <see cref="IsApprovalChainSatisfied"/> is <c>true</c> the proposal
/// has been transitioned to <c>Applied</c> by the issuer (a new
/// Standing Order has been emitted referencing the proposal as
/// <c>supersedes</c> per ADR 0065 §4 rescission semantics — the
/// Issued proposal is NOT mutated in place).
/// </remarks>
/// <param name="IsApprovalChainSatisfied">Whether the §3.1 floor was met by this approval.</param>
/// <param name="ApprovalsGranted">Cumulative count of approvals received on the proposal (including this one).</param>
/// <param name="ApprovalsRequired">Minimum count of approvals required by the active §3.1 floor (≥ 2 for in-Phase-1 deployments).</param>
public sealed record SecurityPolicyApprovalResult(
    bool IsApprovalChainSatisfied,
    int ApprovalsGranted,
    int ApprovalsRequired);

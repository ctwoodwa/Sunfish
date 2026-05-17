namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Verdict returned by
/// <see cref="ISecurityPolicyApprovalFloorProvider.Evaluate"/>. Per
/// ADR 0068 §3.1. When <see cref="AllowApply"/> is <c>true</c> both
/// <see cref="BlockReasonCode"/> + <see cref="BlockReasonAccessibleMessage"/>
/// are <c>null</c>; when <c>false</c> both are non-null.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068. Reason codes are <see cref="string"/> by
/// design (cross-package stability) — do NOT introduce a typed enum
/// that would lock the code set across consumers (council .NET
/// architect ruling from #951 cohort).
/// <para>
/// Canonical reason codes (PR 3b.1):
/// <c>FLOOR_INSUFFICIENT_APPROVERS</c>,
/// <c>FLOOR_NO_SENIOR_APPROVER</c>,
/// <c>FLOOR_SELF_APPROVAL</c>,
/// <c>FLOOR_DUPLICATE_APPROVERS</c>,
/// <c>FLOOR_EXPIRED_PROOF</c>.
/// New codes (post-Phase-1) MUST be additive — never repurpose an
/// existing string.
/// </para>
/// </remarks>
/// <param name="AllowApply">Whether the proposal may transition to <c>Applied</c>.</param>
/// <param name="BlockReasonCode">Stable string code identifying the blocking invariant; <c>null</c> when <see cref="AllowApply"/> is <c>true</c>.</param>
/// <param name="BlockReasonAccessibleMessage">Plain-English explanation per WCAG 3.3.1; <c>null</c> when <see cref="AllowApply"/> is <c>true</c>.</param>
public sealed record ApprovalFloorVerdict(
    bool AllowApply,
    string? BlockReasonCode,
    string? BlockReasonAccessibleMessage);

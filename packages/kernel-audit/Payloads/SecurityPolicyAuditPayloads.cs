using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Audit.Payloads;

/// <summary>
/// Typed payload factories for the
/// <c>Sunfish.SecurityPolicy.*</c> <see cref="AuditEventType"/>
/// cohort per ADR 0068 §6. Mirrors the W#49 / W#50 payload pattern.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Payload records carry the actor + policy-diff summary fields.
/// Sensitive payload values (e.g. raw MFA secrets, raw attestation
/// proof bytes) MUST NEVER appear here — the audit trail is the
/// trans-tenant compliance surface and downstream consumers persist
/// it indefinitely.
/// </para>
/// </remarks>
public static class SecurityPolicyAuditPayloads
{
    /// <summary>Payload for <c>AuditEventType.SecurityPolicyBootstrapped</c>.</summary>
    public sealed record BootstrappedPayload(
        TenantId TenantId,
        DateTimeOffset BootstrappedAt);

    /// <summary>
    /// Payload for <c>AuditEventType.SecurityPolicyProposed</c>.
    /// <see cref="CaptainVacancyExceptionInvoked"/> is set <c>true</c>
    /// when the proposer relied on the ADR 0068 §3.2 Captain-vacancy
    /// elevation (XO satisfies the senior-approver invariant); no new
    /// <c>AuditEventType</c> constant is needed per §3.2 — the
    /// elevation is recorded as a payload flag on the existing event.
    /// Added in PR 3b.1 (W#37 P1).
    /// </summary>
    public sealed record ProposedPayload(
        TenantId TenantId,
        ActorId Proposer,
        StandingOrderId StandingOrderId,
        string PolicyDiffSummary,
        bool CaptainVacancyExceptionInvoked = false);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyApprovalReceived</c>.</summary>
    public sealed record ApprovalReceivedPayload(
        TenantId TenantId,
        StandingOrderId StandingOrderId,
        ActorId Approver,
        DateTimeOffset ApprovedAt);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyApplied</c>.</summary>
    public sealed record AppliedPayload(
        TenantId TenantId,
        StandingOrderId StandingOrderId,
        DateTimeOffset AppliedAt);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyRejected</c>.</summary>
    public sealed record RejectedPayload(
        TenantId TenantId,
        StandingOrderId StandingOrderId,
        string RejectionReason);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyRescinded</c>.</summary>
    public sealed record RescindedPayload(
        TenantId TenantId,
        StandingOrderId StandingOrderId,
        ActorId RescindedBy,
        DateTimeOffset RescindedAt);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyMfaViolation</c>.</summary>
    public sealed record MfaViolationPayload(
        TenantId TenantId,
        ActorId Actor,
        string Role,
        string ViolationKind);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyAttestationViolation</c>.</summary>
    public sealed record AttestationViolationPayload(
        TenantId TenantId,
        ActorId Actor,
        string ClaimedTier,
        string FailureReason,
        bool WasPrivilegedAction);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyKeyRotationOverdue</c>.</summary>
    public sealed record KeyRotationOverduePayload(
        TenantId TenantId,
        ActorId Actor,
        string Role,
        TimeSpan OverdueBy);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyRecoveryContactViolation</c>.</summary>
    public sealed record RecoveryContactViolationPayload(
        TenantId TenantId,
        int CurrentCount,
        int RequiredMinimum);

    /// <summary>Payload for <c>AuditEventType.SecurityPolicyKeyEmergencyRotation</c>.</summary>
    public sealed record KeyEmergencyRotationPayload(
        TenantId TenantId,
        ActorId Actor,
        string Role,
        DateTimeOffset RotatedAt,
        string TriggerReason);
}

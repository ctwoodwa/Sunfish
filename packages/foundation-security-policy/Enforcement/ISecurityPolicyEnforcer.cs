using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Enforcement;

/// <summary>
/// Security-posture gate called by <c>IPermissionResolver</c> as the
/// final step (gate 7 per ADR 0077 §2). After capability-graph checks
/// (steps 1–6) confirm the actor holds the required capability, this
/// gate checks whether the actor's MFA, device attestation, key
/// rotation, and recovery-contact posture meet the tenant's
/// <see cref="TenantSecurityPolicy"/>. Violations produce a
/// <c>PermissionDecision.SecurityPolicyBlocked</c> result per ADR 0068
/// §4.1, surfaced as degraded access via the ADR 0077 §6.6 degradation
/// primitive.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// AUTHORIZATION CONTRACT: This service does NOT consult
/// <c>IUserContext</c>. The caller (typically
/// <c>IPermissionResolver</c>) is the authority on whether an actor
/// is currently authenticated; this enforcer only answers "given the
/// actor's claimed posture, does it satisfy the tenant's policy?"
/// </para>
/// </remarks>
public interface ISecurityPolicyEnforcer
{
    /// <summary>
    /// Check that the actor's MFA enrollment satisfies the tenant's
    /// per-role <see cref="MfaEnrollmentPolicy"/>. Returns
    /// <see cref="PolicyCheckResult.Compliant"/> when the actor's
    /// enrolled factors are a superset of the required factors for
    /// <paramref name="role"/>, or when the actor is within the
    /// enrollment grace period.
    /// </summary>
    ValueTask<PolicyCheckResult> CheckMfaComplianceAsync(
        TenantId tenant,
        ActorId actor,
        ShipRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the actor's <see cref="AttestationEvidence"/> against
    /// the tenant's <see cref="DeviceAttestationPolicy"/>. Hardware-
    /// tier evidence MUST be verified through a registered
    /// <see cref="IAttestationVerifier"/>; absence of a verifier for
    /// the claimed tier fails closed with
    /// <see cref="PolicyViolationKind.DeviceAttestationRequired"/>.
    /// </summary>
    ValueTask<PolicyCheckResult> CheckDeviceAttestationAsync(
        TenantId tenant,
        ActorId actor,
        AttestationEvidence evidence,
        bool isPrivilegedAction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute the key-rotation status for the actor's role given the
    /// tenant's <see cref="KeyRotationPolicy"/>. Phase 1 returns
    /// <see cref="KeyRotationStatus.Current"/> unconditionally — the
    /// key-tracker integration ships in a follow-on.
    /// </summary>
    ValueTask<KeyRotationStatus> GetKeyRotationStatusAsync(
        TenantId tenant,
        ActorId actor,
        ShipRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute recovery-contact compliance for the actor given the
    /// tenant's <see cref="RecoveryContactPolicy"/>. Phase 1 returns
    /// <see cref="RecoveryContactComplianceStatus.Compliant"/>
    /// unconditionally — the recovery-contact-tracker integration
    /// ships in a follow-on.
    /// </summary>
    ValueTask<RecoveryContactComplianceStatus> GetRecoveryContactComplianceAsync(
        TenantId tenant,
        ActorId actor,
        CancellationToken cancellationToken = default);
}

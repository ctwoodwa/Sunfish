using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Validation.Validators;

/// <summary>
/// Priority-200 consistency validator per ADR 0068 §2.1.2:
/// (a) <c>RotationGracePeriod &lt; DefaultRotationCadence</c>;
/// (b) <c>EnrollmentGracePeriod &lt; DefaultRotationCadence</c>;
/// (c) <c>IsReadAtLeastAsPermissiveAsPrivileged == true</c>;
/// (d) <c>KeyRotationTrigger.CompromiseIndicatorFlagged</c> non-
/// removable from <c>AutoTriggers</c> across (proposed, current).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed class ConsistencyValidator : ISecurityPolicyValidator
{
    /// <inheritdoc />
    public SecurityPolicyValidatorPriority Priority => SecurityPolicyValidatorPriority.Consistency;

    /// <inheritdoc />
    public ValueTask<SecurityPolicyValidationResult> ValidateAsync(
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        SecurityPolicyValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(current);
        var findings = new List<SecurityPolicyValidationFinding>();

        if (proposed.KeyRotation.RotationGracePeriod >= proposed.KeyRotation.DefaultRotationCadence)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "CONSISTENCY_KEY_GRACE_GTE_CADENCE",
                "Key rotation grace period must be strictly less than default cadence.",
                "Reduce RotationGracePeriod below DefaultRotationCadence so rotation actually elapses."));

        // MFA <-> KeyRotation coupling rationale: rotating keys mid-MFA-
        // enrollment-grace defeats the grace — the user is told they
        // have N days to enroll, then their key rotates inside that
        // window invalidating prior attempts.
        if (proposed.Mfa.EnrollmentGracePeriod >= proposed.KeyRotation.DefaultRotationCadence)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "CONSISTENCY_MFA_GRACE_GTE_KEY_CADENCE",
                "MFA enrollment grace period must be strictly less than default key rotation cadence.",
                "Reduce EnrollmentGracePeriod below KeyRotationPolicy.DefaultRotationCadence."));

        if (!proposed.DeviceAttestation.IsReadAtLeastAsPermissiveAsPrivileged)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "CONSISTENCY_READ_STRICTER_THAN_PRIVILEGED",
                "Read posture must be at least as permissive as privileged posture.",
                "Ensure AcceptedTiersForReadActions contains every tier in AcceptedTiersForPrivilegedActions (§1.2.2)."));

        // Non-removability of CompromiseIndicatorFlagged — checked across
        // (current, proposed) so a one-shot first proposal that doesn't
        // include it isn't blocked unless the prior policy DID include it.
        var compromiseInCurrent = current.KeyRotation.AutoTriggers.Contains(KeyRotationTrigger.CompromiseIndicatorFlagged);
        var compromiseInProposed = proposed.KeyRotation.AutoTriggers.Contains(KeyRotationTrigger.CompromiseIndicatorFlagged);
        if (compromiseInCurrent && !compromiseInProposed)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "CONSISTENCY_COMPROMISE_TRIGGER_REMOVED",
                "KeyRotationTrigger.CompromiseIndicatorFlagged cannot be removed from AutoTriggers (§1.4.1).",
                "Re-add CompromiseIndicatorFlagged to KeyRotationPolicy.AutoTriggers."));

        return new ValueTask<SecurityPolicyValidationResult>(
            new SecurityPolicyValidationResult(findings));
    }
}

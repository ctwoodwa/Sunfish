using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Validation.Validators;

/// <summary>
/// Priority-100 schema validator per ADR 0068 §2.1.2:
/// non-null required fields, valid enum values, non-negative
/// time-spans, counts &gt;= 1. Compositional record types enforce
/// non-null at construction; this validator catches semantic
/// violations (zero / negative TimeSpan, &lt;1 count, unknown
/// enum-value via numeric assignment).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed class SchemaValidator : ISecurityPolicyValidator
{
    /// <inheritdoc />
    public SecurityPolicyValidatorPriority Priority => SecurityPolicyValidatorPriority.Schema;

    /// <inheritdoc />
    public ValueTask<SecurityPolicyValidationResult> ValidateAsync(
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        SecurityPolicyValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        var findings = new List<SecurityPolicyValidationFinding>();

        // --- AuditRetentionPolicy ---
        var ar = proposed.AuditRetention;
        if (ar.DefaultMinimumRetentionWindow <= TimeSpan.Zero)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RETENTION_MIN_NONPOSITIVE",
                "Default minimum retention window must be greater than zero.",
                "Set DefaultMinimumRetentionWindow to a positive TimeSpan (e.g., 3 years for the Custom preset)."));
        if (ar.DefaultMaximumRetentionWindow < ar.DefaultMinimumRetentionWindow)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RETENTION_MAX_LT_MIN",
                "Default maximum retention window must be greater than or equal to the minimum.",
                "Increase DefaultMaximumRetentionWindow to be at least the minimum window."));
        foreach (var (cls, window) in ar.PerClassOverrides)
        {
            if (!Enum.IsDefined(cls))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_RETENTION_CLASS_UNDEFINED",
                    $"PerClassOverrides key {(int)cls} is not a defined AuditEventClass value.",
                    "Use only defined AuditEventClass values (Security/Financial/Identity/Configuration/System)."));
            if (window.Min <= TimeSpan.Zero)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_RETENTION_PERCLASS_MIN_NONPOSITIVE",
                    $"Per-class minimum window for {cls} must be greater than zero.",
                    $"Set PerClassOverrides[{cls}].Min to a positive TimeSpan."));
            if (window.Max < window.Min)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_RETENTION_PERCLASS_MAX_LT_MIN",
                    $"Per-class maximum window for {cls} is less than its minimum.",
                    $"Increase PerClassOverrides[{cls}].Max to be at least the minimum."));
        }
        if (!Enum.IsDefined(ar.JurisdictionPreset))
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RETENTION_PRESET_UNDEFINED",
                "RetentionJurisdictionPreset is not a defined enum value.",
                "Use one of: Custom, HipaaInformedDefault, PciDssInformedDefault, Soc2InformedDefault, GdprInformedDefault, EuAiActInformedDefault."));

        // --- DeviceAttestationPolicy ---
        var da = proposed.DeviceAttestation;
        foreach (var tier in da.AcceptedTiersForPrivilegedActions.Concat(da.AcceptedTiersForReadActions))
            if (!Enum.IsDefined(tier))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_ATTESTATION_TIER_UNDEFINED",
                    $"AttestationTier {(int)tier} is not defined.",
                    "Use only defined AttestationTier values (None / SoftwareSandbox / AndroidHardwareKeyStore / Tpm2 / AppleSecureElement / Fido2HardwareToken)."));

        // --- MfaEnrollmentPolicy ---
        var mfa = proposed.Mfa;
        if (mfa.EnrollmentGracePeriod < TimeSpan.Zero)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_MFA_GRACE_NEGATIVE",
                "MFA enrollment grace period must be non-negative.",
                "Set EnrollmentGracePeriod to TimeSpan.Zero or a positive value."));
        foreach (var (role, factors) in mfa.RequiredFactorsByRole)
        {
            if (!Enum.IsDefined(role))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_MFA_ROLE_UNDEFINED",
                    $"ShipRole {(int)role} in RequiredFactorsByRole is not defined.",
                    "Use only defined ShipRole values."));
            foreach (var f in factors)
                if (!Enum.IsDefined(f))
                    findings.Add(SecurityPolicyValidationFinding.Error(
                        "SCHEMA_MFA_FACTOR_UNDEFINED",
                        $"MFA factor for role {role} is not a defined MfaFactor value.",
                        "Use only defined MfaFactor values (Totp / WebAuthnPasskey / HardwareKey / Email / Sms)."));
        }

        // --- KeyRotationPolicy ---
        var key = proposed.KeyRotation;
        foreach (var t in key.AutoTriggers)
            if (!Enum.IsDefined(t))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_KEY_TRIGGER_UNDEFINED",
                    $"KeyRotationPolicy.AutoTriggers contains an undefined KeyRotationTrigger value: {t}.",
                    "Use only defined KeyRotationTrigger values."));
        if (key.DefaultRotationCadence <= TimeSpan.Zero)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_KEY_CADENCE_NONPOSITIVE",
                "Default key rotation cadence must be greater than zero.",
                "Set DefaultRotationCadence to a positive TimeSpan (e.g., 90 days)."));
        if (key.RotationGracePeriod < TimeSpan.Zero)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_KEY_GRACE_NEGATIVE",
                "Key rotation grace period must be non-negative.",
                "Set RotationGracePeriod to TimeSpan.Zero or a positive value."));
        foreach (var (role, cadence) in key.PerRoleOverrides)
        {
            if (!Enum.IsDefined(role))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_KEY_ROLE_UNDEFINED",
                    $"ShipRole {(int)role} in PerRoleOverrides is not defined.",
                    "Use only defined ShipRole values."));
            if (cadence <= TimeSpan.Zero)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "SCHEMA_KEY_OVERRIDE_NONPOSITIVE",
                    $"Per-role rotation cadence for {role} must be greater than zero.",
                    $"Set PerRoleOverrides[{role}] to a positive TimeSpan."));
        }

        // --- RecoveryContactPolicy ---
        var rc = proposed.RecoveryContact;
        if (rc.MinimumContactCount < 1)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RECOVERY_MIN_LT_ONE",
                "Recovery-contact MinimumContactCount must be at least 1.",
                "Set MinimumContactCount to 1 or more — at least one recovery contact is required."));
        if (rc.PreferredContactCount < rc.MinimumContactCount)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RECOVERY_PREFERRED_LT_MIN",
                "Recovery-contact PreferredContactCount must be at least MinimumContactCount.",
                "Increase PreferredContactCount to at least MinimumContactCount."));
        if (rc.VerificationCadence <= TimeSpan.Zero)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "SCHEMA_RECOVERY_VERIFY_NONPOSITIVE",
                "Recovery-contact VerificationCadence must be greater than zero.",
                "Set VerificationCadence to a positive TimeSpan (e.g., 90 days)."));

        return new ValueTask<SecurityPolicyValidationResult>(
            new SecurityPolicyValidationResult(findings));
    }
}

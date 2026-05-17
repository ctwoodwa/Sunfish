using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Validation.Validators;

/// <summary>
/// Priority-300 floor validator per ADR 0068 §2.1.2. Implements
/// <see cref="ISecurityPolicyFloorValidator"/> (non-replaceable) so
/// plugins cannot shadow the platform floor. Rules:
/// (a) <c>MinimumContactCount &gt;= 1</c>;
/// (b) <c>ShipRole.Captain</c> MUST NOT have Email-only or Sms-only factors;
/// (c) <c>CompromiseIndicatorFlagged</c> required in <c>AutoTriggers</c>;
/// (d) <c>HipaaInformedDefault</c> floors {Identity, Security,
/// Configuration} at 6 years;
/// (e) <c>PciDssInformedDefault</c> floors {Financial, Security} at 12 months;
/// (f) WCAG 3.3.8 Warning — any role with only cognitive-test factors
/// (Totp/Email/Sms) emits Warning recommending a non-cognitive-test factor.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed class FloorPolicyValidator : ISecurityPolicyFloorValidator
{
    private static readonly TimeSpan SixYears = TimeSpan.FromDays(365 * 6);
    private static readonly TimeSpan TwelveMonths = TimeSpan.FromDays(365);

    private static readonly IReadOnlySet<MfaFactor> CognitiveTestFactors =
        new HashSet<MfaFactor> { MfaFactor.Totp, MfaFactor.Email, MfaFactor.Sms };

    private static readonly IReadOnlySet<MfaFactor> NonCognitiveFactors =
        new HashSet<MfaFactor> { MfaFactor.WebAuthnPasskey, MfaFactor.HardwareKey };

    private static readonly IReadOnlySet<AuditEventClass> HipaaFlooredClasses =
        new HashSet<AuditEventClass> { AuditEventClass.Identity, AuditEventClass.Security, AuditEventClass.Configuration };

    private static readonly IReadOnlySet<AuditEventClass> PciDssFlooredClasses =
        new HashSet<AuditEventClass> { AuditEventClass.Financial, AuditEventClass.Security };

    /// <inheritdoc />
    public SecurityPolicyValidatorPriority Priority => SecurityPolicyValidatorPriority.FloorPolicy;

    /// <inheritdoc />
    public ValueTask<SecurityPolicyValidationResult> ValidateAsync(
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        SecurityPolicyValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        var findings = new List<SecurityPolicyValidationFinding>();

        // (a) RecoveryContact.MinimumContactCount >= 1
        if (proposed.RecoveryContact.MinimumContactCount < 1)
            findings.Add(SecurityPolicyValidationFinding.Error(
                "FLOOR_RECOVERY_MIN_LT_ONE",
                "RecoveryContactPolicy.MinimumContactCount must be at least 1 — at least one recovery contact is required to satisfy key-loss recovery.",
                "Set MinimumContactCount to 1 or more."));

        // (b) Captain MUST NOT have Email-only or Sms-only
        if (proposed.Mfa.RequiredFactorsByRole.TryGetValue(ShipRole.Captain, out var captainFactors))
        {
            var distinct = captainFactors.Distinct().ToList();
            if (distinct.Count == 1 && (distinct[0] == MfaFactor.Email || distinct[0] == MfaFactor.Sms))
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "FLOOR_CAPTAIN_LOW_ASSURANCE_ONLY",
                    $"ShipRole.Captain MUST NOT have {distinct[0]}-only MFA — low-assurance factor not permitted as the sole factor for Captain.",
                    "Add WebAuthnPasskey or HardwareKey to the Captain's MFA factor list (recommended) and retain Totp as a fallback."));
        }

        // (c) CompromiseIndicatorFlagged required (floor invariant — beyond the consistency check)
        if (!proposed.KeyRotation.AutoTriggers.Contains(KeyRotationTrigger.CompromiseIndicatorFlagged))
            findings.Add(SecurityPolicyValidationFinding.Error(
                "FLOOR_COMPROMISE_TRIGGER_REQUIRED",
                "KeyRotationTrigger.CompromiseIndicatorFlagged is required in AutoTriggers — compromise must collapse grace period (§1.4.1).",
                "Add CompromiseIndicatorFlagged to KeyRotationPolicy.AutoTriggers."));

        // (d) HipaaInformedDefault floor
        if (proposed.AuditRetention.JurisdictionPreset == RetentionJurisdictionPreset.HipaaInformedDefault)
            FloorRetentionClasses(proposed.AuditRetention, HipaaFlooredClasses, SixYears,
                findings, "FLOOR_HIPAA_RETENTION_LT_6YR", "6 years");

        // (e) PciDssInformedDefault floor
        if (proposed.AuditRetention.JurisdictionPreset == RetentionJurisdictionPreset.PciDssInformedDefault)
            FloorRetentionClasses(proposed.AuditRetention, PciDssFlooredClasses, TwelveMonths,
                findings, "FLOOR_PCIDSS_RETENTION_LT_12MO", "12 months");

        // (f) WCAG 3.3.8 — Warning when role has only cognitive-test factors
        foreach (var (role, factors) in proposed.Mfa.RequiredFactorsByRole)
        {
            if (factors.Count == 0) continue;
            var hasNonCognitive = factors.Any(NonCognitiveFactors.Contains);
            if (!hasNonCognitive && factors.All(CognitiveTestFactors.Contains))
                findings.Add(SecurityPolicyValidationFinding.Warning(
                    "FLOOR_WCAG_338_COGNITIVE_ONLY",
                    $"ShipRole.{role} has only cognitive-test MFA factors — WCAG 3.3.8 requires an accessible cognitive-test-free path.",
                    $"Add WebAuthnPasskey or HardwareKey to the {role} factor list (the Atlas UI exposes a compliance warning if not enrolled by the actor)."));
        }

        var ok = !findings.Any(f => f.Severity == SecurityPolicyValidationSeverity.Error);
        return new ValueTask<SecurityPolicyValidationResult>(
            new SecurityPolicyValidationResult(ok, findings));
    }

    private static void FloorRetentionClasses(
        AuditRetentionPolicy ar,
        IReadOnlySet<AuditEventClass> classes,
        TimeSpan floor,
        List<SecurityPolicyValidationFinding> findings,
        string code,
        string floorLabel)
    {
        foreach (var cls in classes)
        {
            var min = ar.PerClassOverrides.TryGetValue(cls, out var window)
                ? window.Min
                : ar.DefaultMinimumRetentionWindow;
            if (min < floor)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    code,
                    $"Audit retention for {cls} ({min.TotalDays:F0}d) is below the {floorLabel} floor required by the {ar.JurisdictionPreset} preset.",
                    $"Set PerClassOverrides[{cls}].Min to at least {floorLabel}."));
        }
    }
}

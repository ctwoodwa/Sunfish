using System.Collections.Immutable;
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
    // Calendar-aware floor constants — rounded UP for safety so any
    // typical 6-year window covers the maximum regulatory minimum per
    // 45 CFR §164.530(j)(2) (2191–2192 calendar days), and any 12-
    // month window covers PCI-DSS §10.5.1 (366d covers leap years).
    // Conservative — rejects e.g. a 2191d HIPAA minimum (one-leap-day
    // window) even though HIPAA accepts it; acceptable per §2.1.2
    // coarse semantics. SOC 2 / GDPR / EU AI Act floors are
    // intentionally NOT enforced here: ADR Open-Question 3 documents
    // GDPR as processing-purpose-dependent (no numeric floor), and
    // SOC 2 + EU AI Act floors are RegulatoryValidator scope
    // (priority 400, PR 3).
    private static readonly TimeSpan SixYears = TimeSpan.FromDays(365 * 6 + 2);
    private static readonly TimeSpan TwelveMonths = TimeSpan.FromDays(366);

    private static readonly IReadOnlySet<MfaFactor> CognitiveTestFactors =
        new HashSet<MfaFactor> { MfaFactor.Totp, MfaFactor.Email, MfaFactor.Sms };

    private static readonly IReadOnlySet<MfaFactor> NonCognitiveFactors =
        new HashSet<MfaFactor> { MfaFactor.WebAuthnPasskey, MfaFactor.HardwareKey };

    /// <summary>
    /// Per ADR §1.1.4 / NIST 800-63B Rev. 3 §5.1.3.3 (SMS RESTRICTED).
    /// "Acceptable assurance" excludes Email + Sms. Totp + the two
    /// non-cognitive factors all qualify as at-least-medium assurance.
    /// </summary>
    private static readonly IReadOnlySet<MfaFactor> LowAssuranceFactors =
        new HashSet<MfaFactor> { MfaFactor.Email, MfaFactor.Sms };

    /// <summary>
    /// Roles for which Email-only or Sms-only MFA is forbidden per
    /// ADR 0068 §1.1.4 — Captain, XO, EngineerOfficer must have at
    /// least one phishing-resistant (non-cognitive-test) factor.
    /// </summary>
    private static readonly IReadOnlySet<ShipRole> HighPrivilegeRoles =
        new HashSet<ShipRole> { ShipRole.Captain, ShipRole.XO, ShipRole.EngineerOfficer };

    // Iteration order is deterministic — finding order in
    // result.Findings is stable, supporting downstream snapshot tests
    // + PR 3 issuer rendering. Set semantics aren't load-bearing here
    // (no contains-lookup; only iteration).
    private static readonly ImmutableArray<AuditEventClass> HipaaFlooredClasses =
        ImmutableArray.Create(AuditEventClass.Identity, AuditEventClass.Security, AuditEventClass.Configuration);

    private static readonly ImmutableArray<AuditEventClass> PciDssFlooredClasses =
        ImmutableArray.Create(AuditEventClass.Financial, AuditEventClass.Security);

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

        // (a) RecoveryContact.MinimumContactCount >= 1 — REMOVED in
        // backfill per xo-council B3. This is a schema invariant
        // (structural nonsense), not a jurisdictional floor; the
        // SchemaValidator owns the check (SCHEMA_RECOVERY_MIN_LT_ONE).
        // Eliminating the duplicate prevents one violation from
        // producing two findings with different codes.

        // (b) Captain + XO + EngineerOfficer MUST NOT have Email-only,
        // Sms-only, or combined-low-assurance ([Email, Sms]) MFA — per
        // ADR §1.1.4 + NIST SP 800-63B Rev. 3 §5.1.3.3 (SMS RESTRICTED).
        // Totp is acceptable for these roles (cognitive-test surfaces
        // a separate WCAG 3.3.8 Warning at rule (f) below; that warning
        // does not block these roles' configurations).
        foreach (var role in HighPrivilegeRoles)
        {
            if (!proposed.Mfa.RequiredFactorsByRole.TryGetValue(role, out var factors)) continue;
            if (factors.Count == 0) continue;
            // Error when EVERY factor is low-assurance (Email or Sms);
            // a single Totp or non-cognitive factor disqualifies the
            // error. This catches "[Email]" / "[Sms]" / "[Email, Sms]"
            // while permitting "[Totp]" + any superset.
            var allLowAssurance = factors.All(LowAssuranceFactors.Contains);
            if (allLowAssurance)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    "FLOOR_HIGH_PRIV_LOW_ASSURANCE_ONLY",
                    $"ShipRole.{role} MUST NOT have Email-only or Sms-only MFA (current: [{string.Join(", ", factors)}]). Per ADR §1.1.4 / NIST SP 800-63B §5.1.3.3 (SMS RESTRICTED).",
                    $"Add at least one of Totp / WebAuthnPasskey / HardwareKey to the {role} factor list. WebAuthnPasskey is preferred (also satisfies WCAG 3.3.8)."));
        }

        // TODO(W#37 PR3): EmergencyOverride rate-limit (1/24h per actor)
        // is a §2.1.2(f) floor rule deferred to PR 3 because rate-limit
        // enforcement requires actor + timestamp state that the
        // current ValidateAsync signature doesn't carry. The PR 3
        // issuer will compose this validator with an
        // ISecurityPolicyOverrideRateLimiter and enforce there.

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

        return new ValueTask<SecurityPolicyValidationResult>(
            new SecurityPolicyValidationResult(findings));
    }

    private static void FloorRetentionClasses(
        AuditRetentionPolicy ar,
        IReadOnlyList<AuditEventClass> classes,
        TimeSpan floor,
        List<SecurityPolicyValidationFinding> findings,
        string codePrefix,
        string floorLabel)
    {
        foreach (var cls in classes)
        {
            var min = ar.PerClassOverrides.TryGetValue(cls, out var window)
                ? window.Min
                : ar.DefaultMinimumRetentionWindow;
            if (min < floor)
                findings.Add(SecurityPolicyValidationFinding.Error(
                    // Class-distinguishing code so downstream tooling
                    // (PR 3 issuer, Atlas UI) doesn't dedup 3 distinct
                    // class-level violations into a single finding.
                    $"{codePrefix}_{cls.ToString().ToUpperInvariant()}",
                    $"Audit retention for {cls} ({min.TotalDays:F0}d) is below the {floorLabel} floor required by the {ar.JurisdictionPreset} preset.",
                    $"Set PerClassOverrides[{cls}].Min to at least {floorLabel}."));
        }
    }
}

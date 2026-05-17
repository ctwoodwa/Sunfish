using System.Collections.ObjectModel;

namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Per-class audit-retention windows + jurisdiction preset per
/// ADR 0068 §1.3. The <c>kernel-audit</c> expiry path consults this
/// policy at purge time; the floor validator (subsequent PR) enforces
/// preset-derived minimum windows
/// (e.g., <see cref="RetentionJurisdictionPreset.HipaaInformedDefault"/>
/// floors Identity/Security/Configuration at 6 years).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// RIGHT-TO-ERASURE: Sunfish audit records are append-only per ADR
/// 0049. Erasure requests against audit records during a mandatory
/// minimum window require legal sign-off and manual operator action.
/// Sunfish does NOT expose a "delete audit record" endpoint. Whether
/// GDPR Article 17(3)(b) or another legal-obligation exemption applies
/// is a per-deployment legal determination — see §GC.1 + §5.1.
/// </para>
/// </remarks>
public sealed record AuditRetentionPolicy(
    TimeSpan DefaultMinimumRetentionWindow,
    TimeSpan DefaultMaximumRetentionWindow,
    IReadOnlyDictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)> PerClassOverrides,
    RetentionJurisdictionPreset JurisdictionPreset)
{
    public static readonly AuditRetentionPolicy Default = new(
        DefaultMinimumRetentionWindow: TimeSpan.FromDays(365 * 3),
        DefaultMaximumRetentionWindow: TimeSpan.FromDays(365 * 7),
        PerClassOverrides: new ReadOnlyDictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>(
            new Dictionary<AuditEventClass, (TimeSpan Min, TimeSpan Max)>()),
        JurisdictionPreset: RetentionJurisdictionPreset.Custom);
}

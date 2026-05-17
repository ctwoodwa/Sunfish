using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Retention;

/// <summary>
/// §5.2 jurisdiction-floor logic shared between
/// <see cref="DefaultRetentionPolicyResolver"/> and
/// <see cref="DefaultAuditRetentionEnforcer"/>. Maps
/// <c>(RetentionJurisdictionPreset, AuditEventClass)</c> to the
/// minimum hold window the preset mandates (or <c>null</c> when the
/// preset does NOT mandate a floor for the class).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Floor values are informed by published interpretations of the
/// named regulatory regimes; deployers MUST obtain qualified legal
/// counsel before relying on these values for compliance attestation.
/// <para>
/// Floors per §5.2:
/// </para>
/// <list type="bullet">
///   <item><see cref="RetentionJurisdictionPreset.HipaaInformedDefault"/> — floors <c>{Identity, Security, Configuration}</c> at 6 years (leap-day-aware: <c>365 * 6 + 2 = 2192 days</c>, matches PR #948 R1)</item>
///   <item><see cref="RetentionJurisdictionPreset.PciDssInformedDefault"/> — floors <c>{Financial, Security}</c> at 12 months (leap-year-safe: <c>366 days</c>, matches PR #948 R2)</item>
///   <item><see cref="RetentionJurisdictionPreset.Soc2InformedDefault"/> — NO mandatory floor ("informed baseline only" per §5.2)</item>
///   <item><see cref="RetentionJurisdictionPreset.GdprInformedDefault"/> — NO auto-derived floor (per OQ-3 — manual per-class config required)</item>
///   <item><see cref="RetentionJurisdictionPreset.EuAiActInformedDefault"/> — NO auto-applied floor (HIGH-RISK SYSTEMS need 10 years; consult counsel before enabling, per §1.3 enum docstring)</item>
///   <item><see cref="RetentionJurisdictionPreset.Custom"/> — NO floor applied</item>
/// </list>
/// </remarks>
public static class JurisdictionFloorHelper
{
    /// <summary>HIPAA §164.530(j)(2) — 6 calendar years inclusive of leap days. Matches PR #948 R1.</summary>
    public static readonly System.TimeSpan HipaaSixYears = System.TimeSpan.FromDays(365 * 6 + 2);

    /// <summary>PCI-DSS §10.5.1 — 12 months leap-year-safe. Matches PR #948 R2.</summary>
    public static readonly System.TimeSpan PciDssTwelveMonths = System.TimeSpan.FromDays(366);

    /// <summary>
    /// Compute the minimum-hold floor (if any) that the
    /// <paramref name="preset"/> mandates for the given
    /// <paramref name="eventClass"/>. Returns <c>null</c> when no
    /// preset-derived floor applies.
    /// </summary>
    public static System.TimeSpan? GetFloor(RetentionJurisdictionPreset preset, AuditEventClass eventClass) => preset switch
    {
        RetentionJurisdictionPreset.HipaaInformedDefault
            when eventClass is AuditEventClass.Identity
                            or AuditEventClass.Security
                            or AuditEventClass.Configuration => HipaaSixYears,

        RetentionJurisdictionPreset.PciDssInformedDefault
            when eventClass is AuditEventClass.Financial
                            or AuditEventClass.Security => PciDssTwelveMonths,

        // SOC 2, GDPR, EU AI Act, Custom — no auto-derived floor.
        _ => null,
    };
}

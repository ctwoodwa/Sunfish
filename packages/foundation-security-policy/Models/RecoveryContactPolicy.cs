namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Recovery-contact enrollment minimum / preferred + verification
/// cadence + new-tenant enrollment deadline per ADR 0068 §1.5.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Floor validator (subsequent PR) enforces
/// <see cref="MinimumContactCount"/> &gt;= 1.
/// </para>
/// </remarks>
public sealed record RecoveryContactPolicy(
    int MinimumContactCount,
    int PreferredContactCount,
    TimeSpan VerificationCadence,
    TimeSpan EnrollmentDeadlineForNewTenants)
{
    public static readonly RecoveryContactPolicy Default = new(
        MinimumContactCount: 1,
        PreferredContactCount: 3,
        VerificationCadence: TimeSpan.FromDays(90),
        EnrollmentDeadlineForNewTenants: TimeSpan.FromDays(30));
}

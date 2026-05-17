namespace Sunfish.Kernel.Audit.Retention;

/// <summary>
/// Whether <see cref="IAuditRetentionEnforcer"/> should actually purge
/// matching entries or merely report what would be purged.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention enforcement intersects HIPAA, PCI-DSS, SOC 2, GDPR, and
/// the EU AI Act. The thresholds + enforcement modes here are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before enabling
/// <see cref="Active"/> mode in production.
/// </remarks>
public enum AuditRetentionEnforcementMode
{
    /// <summary>Compute what would be purged + return the stats; do NOT delete anything.</summary>
    DryRun,

    /// <summary>Purge entries that exceed the retention window AND are not under legal hold.</summary>
    Active,
}

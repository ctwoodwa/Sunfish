namespace Sunfish.Kernel.Audit.Retention;

/// <summary>
/// Per-tenant retention policy applied to the audit log by
/// <see cref="IAuditRetentionEnforcer"/>. Resolved upstream from the
/// tenant's <c>TenantSecurityPolicy</c> (see
/// <c>Sunfish.Foundation.SecurityPolicy</c>) and translated into the
/// concrete day-window + hold-override + enforcement-mode contract
/// the kernel-audit layer can act on.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention thresholds in this record are informed by HIPAA,
/// PCI-DSS, SOC 2, GDPR, and the EU AI Act floors; the exact values
/// MUST be reviewed by qualified legal counsel for the deployer's
/// jurisdiction before production use.
/// <para>
/// <b>Why the kernel owns this record.</b> Retention purge is a
/// kernel-audit concern (the audit log is the kernel substrate); the
/// per-tenant variation is a security-policy concern. The interface
/// + record live in kernel-audit so the dependency arrow points
/// foundation-security-policy → kernel-audit (concerns flow toward
/// the substrate).
/// </para>
/// </remarks>
/// <param name="MinDays">Minimum days an audit record MUST be retained before purge becomes eligible. Floors take precedence over per-class overrides per ADR 0068 §5.2.</param>
/// <param name="MaxDays">Maximum days an audit record MAY be retained before purge becomes mandatory under the deployer's policy. <c>int.MaxValue</c> means "keep indefinitely" (no max enforced).</param>
/// <param name="LegalHoldOverride">When <c>true</c>, the <see cref="EnforcementMode"/> is ignored and NO entries are purged regardless of age — supports legal-hold scenarios where the tenant has been notified of a compliance / discovery freeze.</param>
/// <param name="EnforcementMode">Whether the enforcer purges (<see cref="AuditRetentionEnforcementMode.Active"/>) or only reports (<see cref="AuditRetentionEnforcementMode.DryRun"/>).</param>
public sealed record AuditRetentionPolicy(
    int MinDays,
    int MaxDays,
    bool LegalHoldOverride,
    AuditRetentionEnforcementMode EnforcementMode);

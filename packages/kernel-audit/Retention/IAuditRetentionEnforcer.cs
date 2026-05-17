using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Audit.Retention;

/// <summary>
/// Per-tenant retention enforcement against the audit log per ADR
/// 0049 + ADR 0068 §1.6 + §5. Resolves the caller-supplied
/// <see cref="AuditRetentionPolicy"/> against the kernel-audit
/// underlying store and either purges (Active) or reports (DryRun)
/// the entries that exceed the retention window.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention enforcement intersects HIPAA, PCI-DSS, SOC 2, GDPR, and
/// the EU AI Act. The thresholds in <see cref="AuditRetentionPolicy"/>
/// are informed defaults, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement for
/// production use.
/// <para>
/// <b>Per-tenant scope.</b> Every invocation MUST be scoped to a
/// single tenant — cross-tenant sweeps are explicitly out of scope
/// for the kernel-audit interface (the §5.2 jurisdiction-floor
/// translation is per-tenant in the upstream
/// <c>Sunfish.Foundation.SecurityPolicy</c> resolver).
/// </para>
/// <para>
/// <b>Audit-by-construction.</b> Implementations SHOULD emit an
/// <c>AuditEventType.AuditRetentionSwept</c> (or equivalent) record
/// at the end of each <see cref="ApplyAsync"/> invocation —
/// regardless of <see cref="AuditRetentionEnforcementMode"/> — so
/// the audit trail itself records every retention decision. (The
/// concrete event-type constant lands with the implementation in
/// PR 3b.2.)
/// </para>
/// <para>
/// <b>Legal hold.</b> When
/// <see cref="AuditRetentionPolicy.LegalHoldOverride"/> is set,
/// implementations MUST treat the call as a no-op-with-stats: count
/// what would have been purged into
/// <see cref="RetentionEnforcementResult.EntriesSkippedDueToHold"/>
/// + return without mutating the log.
/// </para>
/// </remarks>
public interface IAuditRetentionEnforcer
{
    /// <summary>
    /// Apply the supplied retention policy to the audit records of
    /// <paramref name="tenant"/>. Returns stats describing what the
    /// enforcer evaluated + purged + held.
    /// </summary>
    /// <param name="tenant">Tenant whose audit records are to be evaluated.</param>
    /// <param name="policy">Per-tenant retention policy as resolved upstream by <c>Sunfish.Foundation.SecurityPolicy.Retention</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RetentionEnforcementResult> ApplyAsync(
        TenantId tenant,
        AuditRetentionPolicy policy,
        CancellationToken cancellationToken = default);
}

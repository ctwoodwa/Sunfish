namespace Sunfish.Kernel.Audit.Retention;

/// <summary>
/// Outcome of a single <see cref="IAuditRetentionEnforcer.ApplyAsync"/>
/// invocation.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068. The kernel-audit retention enforcer is
/// the only path the audit log surface mutates outside of the
/// append-only write path; result records ARE the forensic trail of
/// every retention sweep.
/// </remarks>
/// <param name="PolicyMatched"><c>true</c> when the policy resolved to a non-default retention window for the tenant; <c>false</c> when no policy was found and the enforcer no-oped.</param>
/// <param name="EntriesEvaluated">Total count of audit records the enforcer inspected during the sweep.</param>
/// <param name="EntriesPurged">Count of entries actually purged. Always <c>0</c> when <see cref="AuditRetentionPolicy.EnforcementMode"/> is <see cref="AuditRetentionEnforcementMode.DryRun"/>.</param>
/// <param name="EntriesSkippedDueToHold">Count of entries the enforcer would have purged but didn't because <see cref="AuditRetentionPolicy.LegalHoldOverride"/> was set OR a per-entry hold flag was found upstream.</param>
public sealed record RetentionEnforcementResult(
    bool PolicyMatched,
    int EntriesEvaluated,
    int EntriesPurged,
    int EntriesSkippedDueToHold);

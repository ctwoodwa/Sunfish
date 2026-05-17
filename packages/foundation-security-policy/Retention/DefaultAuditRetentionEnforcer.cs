using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit.Retention;
using KernelPolicy = Sunfish.Kernel.Audit.Retention.AuditRetentionPolicy;

namespace Sunfish.Foundation.SecurityPolicy.Retention;

/// <summary>
/// Reference implementation of
/// <see cref="IAuditRetentionEnforcer"/> from <c>kernel-audit</c>.
/// Drives the per-tenant retention sweep by composing
/// <see cref="IRetentionPolicyResolver"/> for per-class window
/// resolution. Phase 1 does NOT actually purge audit records — the
/// kernel-audit log is append-only per ADR 0049 + has no purge surface
/// in this phase. The enforcer reports what would-be-purged
/// (DryRun-equivalent statistics regardless of
/// <see cref="AuditRetentionEnforcementMode"/>) and emits an
/// <c>AuditEventType.AuditRetentionSwept</c>-style audit record (constant
/// addition is a follow-on; PR 3b.2 omits it pending kernel-audit cohort
/// agreement).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Retention enforcement intersects HIPAA, PCI-DSS, SOC 2, GDPR, and
/// the EU AI Act. Deployers MUST obtain qualified legal counsel
/// before relying on this enforcer in production.
/// <para>
/// <b>Phase 1 limitations</b> (tracked for follow-on hand-offs):
/// </para>
/// <list type="bullet">
///   <item>No purge surface on <see cref="Sunfish.Kernel.Audit.IAuditTrail"/> yet — enforcer reports counts only; no records are deleted regardless of <see cref="AuditRetentionEnforcementMode"/>.</item>
///   <item>No <c>AuditRetentionSwept</c> <c>AuditEventType</c> constant yet — sweep is not self-recording; observability comes via host-side logging until the constant lands.</item>
///   <item>Single-process sweep — concurrent sweep callers across a cluster have no coordination; recommend single-leader at deployment time.</item>
/// </list>
/// </remarks>
public sealed class DefaultAuditRetentionEnforcer : IAuditRetentionEnforcer
{
    private readonly IRetentionPolicyResolver _resolver;
    private readonly TimeProvider _time;

    /// <summary>Construct bound to the per-tenant resolver + a time provider.</summary>
    public DefaultAuditRetentionEnforcer(
        IRetentionPolicyResolver resolver,
        TimeProvider time)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public async Task<RetentionEnforcementResult> ApplyAsync(
        TenantId tenant,
        KernelPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // Probe the per-class resolver for every AuditEventClass so the resolver
        // has been exercised against the tenant's projection at sweep time. This
        // is the cohort-level "verify the tenant has an active policy AND every
        // event class resolves cleanly" check. The verdicts themselves are
        // discarded — Phase 1 does not consume them because kernel-audit has no
        // purge surface yet (see class remarks).
        var policyMatched = false;
        try
        {
            foreach (var eventClass in Enum.GetValues<Models.AuditEventClass>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = await _resolver.ResolveAsync(tenant, eventClass, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            }
            policyMatched = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Resolver-side failure (loader-failure, malformed policy) ⇒ report
            // not-matched so the caller can decide whether to escalate. The
            // exception is suppressed because the sweep must remain idempotent;
            // a partial result is preferable to a crash that would block the
            // scheduled sweeper from progressing on other tenants. Host-side
            // logging is the channel for the actual diagnostic.
            policyMatched = false;
        }

        // Phase 1: no purge surface on IAuditTrail yet (ADR 0049 append-only).
        // All counts report 0; legal-hold semantics light up in a follow-on PR
        // once the kernel-audit purge contract lands.
        // TODO(PR 3b.x): when purge surface lands, populate EntriesEvaluated /
        // EntriesPurged from the actual sweep + set EntriesSkippedDueToHold to
        // the count of entries that would have been purged but weren't due to
        // policy.LegalHoldOverride.
        _ = policy;   // parameter intent stays visible until the purge surface lands

        return new RetentionEnforcementResult(
            PolicyMatched: policyMatched,
            EntriesEvaluated: 0,
            EntriesPurged: 0,
            EntriesSkippedDueToHold: 0);
    }
}

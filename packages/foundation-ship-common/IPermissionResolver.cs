using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Authoritative role-aware policy gate per ADR 0077 §2. Sits ABOVE
/// <see cref="ICapabilityGraph"/> — translates <see cref="ShipAction"/> →
/// <see cref="CapabilityAction"/>, queries the graph, and composes role +
/// location + deck + Mission-Envelope + security-policy checks into a single
/// structured <see cref="PermissionDecision"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="PermissionDecision.Denied"/> emits an
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.PermissionDenied"/>
/// audit record. Granted decisions audit only when the action is in
/// <see cref="AuditLoudActions"/>; the rest are silent to avoid audit-log
/// flooding on routine reads.
/// </para>
/// <para>
/// Implementations MUST enforce the <c>(ActorId, ShipLocation)</c> denial
/// rate limit per §2.4 — N=10 denials within a 1-minute sliding window
/// emits a single
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.PermissionDenialRateExceeded"/>
/// record and returns <see cref="PermissionDecision.Denied"/> with
/// <see cref="DenialReason.SecurityPolicyBlocked"/> for subsequent calls
/// within the window.
/// </para>
/// </remarks>
public interface IPermissionResolver
{
    /// <summary>
    /// Resolve the permission tuple per ADR 0077 §2.1 (steps 0–8). Never
    /// returns a bare bool — every resolution carries a reason + remediation
    /// so the First-Aid denial UX has the data it needs.
    /// </summary>
    /// <remarks>
    /// <b>Tenant binding (§Trust).</b> The W#46 P1 pre-merge security
    /// council 2026-05-06 found that the ADR 0077 §2 sketch signature
    /// without an explicit <see cref="TenantId"/> permits a cross-tenant
    /// authority bleed: a principal who holds Captain in tenant-A and
    /// DivisionOfficer in tenant-B could be granted Captain authority for
    /// tenant-B-scoped calls if the cache iterates tenant-A first. This
    /// API takes <paramref name="tenantId"/> explicitly so the resolver
    /// looks up assignments only within the caller-asserted tenant. Cohort
    /// precedent: <c>IOodWatchService.StartWatchAsync</c> +
    /// <c>GetActiveWatchAsync</c> both take <see cref="TenantId"/> as the
    /// first parameter.
    /// </remarks>
    /// <param name="tenantId">Tenant scope of the resolution; the resolver looks up assignments only within this tenant.</param>
    /// <param name="subject">The principal whose authority is being resolved.</param>
    /// <param name="location">Department location per <see cref="ShipLocation"/>.</param>
    /// <param name="deck">
    /// Caller-supplied deck depth. The resolver canonicalizes this via
    /// step 0(a) — callers MUST NOT be trusted to self-report action
    /// sensitivity.
    /// </param>
    /// <param name="action">The <see cref="ShipAction"/> being attempted.</param>
    /// <param name="resource">
    /// Resource reference for resource-scoped actions (<see cref="ShipAction.Approve"/>,
    /// <see cref="ShipAction.Quarantine"/>, <see cref="ShipAction.OverrideQuarantine"/>);
    /// null for location-scoped actions per §2.0. Passing null for a
    /// resource-scoped action returns <see cref="PermissionDecision.Denied"/>
    /// at step 0(c).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<PermissionDecision> ResolveAsync(
        TenantId tenantId,
        Principal subject,
        ShipLocation location,
        DeckDepth deck,
        ShipAction action,
        Resource? resource,
        CancellationToken ct = default);

    /// <summary>
    /// Static-readonly enumeration of <see cref="ShipAction"/> values whose
    /// <see cref="PermissionDecision.Granted"/> outcome ALSO emits an audit
    /// record per ADR 0077 §2.4 (audit-loud set: below-the-waterline
    /// actions, watch transfers, role promotions, escalation approvals).
    /// Routine reads/writes are NOT audit-loud — auditing every grant would
    /// drown the audit log in noise.
    /// </summary>
    IReadOnlyList<ShipAction> AuditLoudActions { get; }
}

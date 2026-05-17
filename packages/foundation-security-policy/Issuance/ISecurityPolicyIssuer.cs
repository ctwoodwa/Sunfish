using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Issues, approves, and rescinds tenant security-policy changes. Per
/// ADR 0068 §3. Composes
/// <c>Sunfish.Foundation.Wayfinder.IStandingOrderIssuer</c> for the
/// underlying Standing Order emission + state transitions.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// <b>Audit-by-construction.</b> Every successful + every rejected
/// invocation emits exactly one
/// <see cref="Sunfish.Kernel.Audit.AuditEventType"/> record from the
/// <c>Sunfish.SecurityPolicy.*</c> cohort. Implementations MUST NOT
/// short-circuit the audit emission on the failure path — audit
/// completeness is a §6.6 ADR 0077 invariant.
/// </para>
/// <para>
/// <b>Authorization contract.</b> This service does NOT consult
/// <c>IUserContext</c>. The caller (typically a UI command handler)
/// is the authority on whether an actor is currently authenticated;
/// this issuer enforces only the §3.1 ApprovalChain floor + §3.1.1
/// CapabilityProof freshness against the actor identities supplied.
/// </para>
/// </remarks>
public interface ISecurityPolicyIssuer
{
    /// <summary>
    /// Propose a security-policy change. The underlying Standing Order
    /// enters <c>Issued</c> state; it does NOT take effect until the
    /// §3.1 approval chain is satisfied via
    /// <see cref="ApproveAsync"/>. Emits
    /// <c>AuditEventType.SecurityPolicyProposed</c> on success;
    /// <c>SecurityPolicyRejected</c> on validation-pipeline failure.
    /// </summary>
    /// <param name="tenant">The tenant whose policy is being proposed for change.</param>
    /// <param name="proposer">The actor issuing the proposal.</param>
    /// <param name="proposed">The full proposed policy snapshot.</param>
    /// <param name="rationale">Operator-supplied free-text rationale; required for audit / forensic review.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="StandingOrderId"/> of the Issued proposal.</returns>
    ValueTask<StandingOrderId> ProposeAsync(
        TenantId tenant,
        ActorId proposer,
        TenantSecurityPolicy proposed,
        string rationale,
        CancellationToken ct = default);

    /// <summary>
    /// Record a co-approval against a previously
    /// <see cref="ProposeAsync"/>-issued proposal. When the §3.1 floor
    /// is satisfied by this approval the issuer transitions the
    /// proposal to <c>Applied</c> via a new Standing Order
    /// referencing the proposal as <c>supersedes</c> (ADR 0065 §4 —
    /// the Issued proposal is NOT mutated in place). Emits
    /// <c>AuditEventType.SecurityPolicyApprovalReceived</c> for every
    /// approval; emits <c>SecurityPolicyApplied</c> on transition;
    /// emits <c>SecurityPolicyRejected</c> on stale / mismatched
    /// proof / floor-block.
    /// </summary>
    /// <param name="tenant">The tenant whose proposal is being approved.</param>
    /// <param name="approver">The actor approving the proposal. MUST differ from the proposer (§3.1 invariant 3).</param>
    /// <param name="proposal">The <see cref="StandingOrderId"/> returned by <see cref="ProposeAsync"/>.</param>
    /// <param name="approverProof">Per-approver capability proof bound to <paramref name="proposal"/> as nonce. MUST be fresh per §3.1.1 (default 24h).</param>
    /// <param name="comment">Optional operator-supplied free-text comment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SecurityPolicyApprovalResult"/> describing the post-approval chain state.</returns>
    ValueTask<SecurityPolicyApprovalResult> ApproveAsync(
        TenantId tenant,
        ActorId approver,
        StandingOrderId proposal,
        CapabilityProof approverProof,
        string? comment,
        CancellationToken ct = default);

    /// <summary>
    /// Rescind a pending proposal. Only the original
    /// <see cref="ProposeAsync"/>-proposer OR an actor holding
    /// <see cref="Sunfish.Foundation.Ship.Common.ShipRole.Captain"/>
    /// may rescind (per §3.1). Rescission is only permitted while the
    /// underlying Standing Order is in <c>Issued</c> state — once
    /// <c>Applied</c>, withdrawing requires a fresh proposal +
    /// approval cycle. Emits
    /// <c>AuditEventType.SecurityPolicyRescinded</c>.
    /// </summary>
    /// <param name="tenant">The tenant whose proposal is being rescinded.</param>
    /// <param name="actor">The actor performing the rescission.</param>
    /// <param name="proposal">The <see cref="StandingOrderId"/> to rescind.</param>
    /// <param name="reason">Operator-supplied free-text reason; required for audit / forensic review.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask RescindAsync(
        TenantId tenant,
        ActorId actor,
        StandingOrderId proposal,
        string reason,
        CancellationToken ct = default);
}

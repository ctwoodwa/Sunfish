using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Issues and rescinds <see cref="StandingOrder"/> instances. Per ADR 0065 §4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Audit-by-construction.</b> Both <see cref="IssueAsync"/> and
/// <see cref="RescindAsync"/> require an <see cref="IAuditTrail"/> dependency
/// — the issuance / rescission MUST emit an <c>AuditRecord</c>. The audit
/// emission is non-optional at the type level so a configuration change
/// without an audit footprint is impossible to construct.
/// </para>
/// <para>
/// <b>Rescission semantics.</b> <see cref="RescindAsync"/> emits a new
/// <c>AuditRecord</c> with <see cref="AuditEventType.StandingOrderRescinded"/>
/// referencing the rescinded id; it does NOT redact the original
/// <c>StandingOrderIssued</c> record (audit immutability per ADR 0049 is
/// preserved). The rescission nullifies the future effect of the rescinded
/// order on the Atlas projection only.
/// </para>
/// </remarks>
public interface IStandingOrderIssuer
{
    /// <summary>
    /// Run the validation chain against <paramref name="draft"/>, persist the
    /// resulting <see cref="StandingOrder"/> via
    /// <see cref="IStandingOrderRepository"/>, and emit a
    /// <see cref="AuditEventType.StandingOrderIssued"/> (or
    /// <see cref="AuditEventType.StandingOrderRejected"/> on Block-severity
    /// validation failure) record via <paramref name="auditTrail"/>.
    /// </summary>
    /// <param name="draft">Caller-supplied draft.</param>
    /// <param name="issuedBy">Actor performing the issuance.</param>
    /// <param name="auditTrail">Audit trail to emit the issuance record into. Required (not optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The realized order, with issuer-set fields populated.</returns>
    Task<StandingOrder> IssueAsync(
        StandingOrderDraft draft,
        ActorId issuedBy,
        IAuditTrail auditTrail,
        CancellationToken ct);

    /// <summary>
    /// Rescind an existing Standing Order. Emits a new audit record
    /// (<see cref="AuditEventType.StandingOrderRescinded"/>) referencing the
    /// rescinded id; does not redact the original issuance audit record.
    /// </summary>
    /// <param name="id">The order to rescind.</param>
    /// <param name="rescindedBy">Actor performing the rescission.</param>
    /// <param name="rationale">Operator-supplied free-text rationale; required for audit / forensic review.</param>
    /// <param name="auditTrail">Audit trail to emit the rescission record into. Required (not optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rescinded order, with <see cref="StandingOrder.State"/> set to <see cref="StandingOrderState.Rescinded"/>.</returns>
    Task<StandingOrder> RescindAsync(
        StandingOrderId id,
        ActorId rescindedBy,
        string rationale,
        IAuditTrail auditTrail,
        CancellationToken ct);
}

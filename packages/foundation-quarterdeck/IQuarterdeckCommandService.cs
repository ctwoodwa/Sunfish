using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Command surface for Quarterdeck operations per ADR 0080 §2 + §5.
/// Phase 1 ships the contract; the
/// <c>DefaultQuarterdeckCommandService</c> implementation lands in
/// Phase 2 with security wiring.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-phase audit (§5):</b>
/// <see cref="AcknowledgeAlertAsync"/> emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.AlertAcknowledgementRequested"/>
/// (intent / pre-op) BEFORE the underlying alert source is asked to
/// acknowledge, then emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.AlertAcknowledged"/>
/// on success. Failure is signalled by absence of the post-op event
/// (no separate <c>AlertAcknowledgementFailed</c> constant — the
/// audit-trail diff IS the failure record).
/// </para>
/// <para>
/// <b>Authority check (§5):</b> implementations MUST resolve
/// <c>ShipAction.AcknowledgeAlert</c> via
/// <see cref="Sunfish.Foundation.Ship.Common.IPermissionResolver"/>
/// before delegating to the alert source. Denied requests still emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.AlertAcknowledgementRequested"/>
/// (intent is auditable) but skip the post-op event and surface the
/// denial via First-Aid.
/// </para>
/// </remarks>
public interface IQuarterdeckCommandService
{
    /// <summary>
    /// Acknowledge a pending alert. Returns <c>true</c> when the
    /// underlying alert source confirmed acknowledgement; <c>false</c>
    /// when the request was denied or the source rejected the
    /// acknowledgement. Tenant binding + authority check apply per the
    /// remarks.
    /// </summary>
    ValueTask<bool> AcknowledgeAlertAsync(
        string alertId,
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Write-side surface for Tactical operations per ADR 0081 §2 + §8.
/// Phase 1 ships the contract; the
/// <c>DefaultTacticalCommandService</c> implementation lands in
/// Phase 2 with security wiring.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-phase audit (§8):</b> every command emits its <c>*Requested</c>
/// pre-op event as the FIRST observable side-effect — before the §8.2
/// tenant-binding check, before resource-existence resolution, before
/// permission resolution, and before any First-Aid surface — and emits
/// the corresponding <c>*Acknowledged</c> / <c>*Opened</c> /
/// <c>*Closed</c> post-op event ONLY on success. Absence of the
/// post-op event in the audit trail IS the failure record. The only
/// acceptable reason to skip pre-op emission is failure of the audit
/// infrastructure itself, which surfaces as a thrown exception (and
/// therefore aborts the command before any state change); a silent
/// catch-and-continue MUST NOT be implemented.
/// </para>
/// <para>
/// <b>Authority check (§8):</b> implementations MUST resolve the
/// requisite <c>Sunfish.Foundation.Ship.Common.ShipAction</c>
/// via <c>IPermissionResolver</c> AFTER the pre-op audit emission;
/// denied requests skip the post-op event and throw
/// <see cref="TacticalUnauthorizedException"/>.
/// </para>
/// </remarks>
public interface ITacticalCommandService
{
    /// <summary>
    /// Acknowledge a Lookout alert. Requires
    /// <c>ShipAction.AcknowledgeTacticalAlert</c>. Emits
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAlertAcknowledgementRequested"/>
    /// pre-op + <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAlertAcknowledged"/>
    /// post-op.
    /// </summary>
    ValueTask AcknowledgeAlertAsync(
        TenantId tenantId,
        Principal actor,
        string alertId,
        CancellationToken ct = default);

    /// <summary>
    /// Open a new incident from a root alert. Requires
    /// <c>ShipAction.OpenIncident</c>. Emits
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.IncidentOpenRequested"/>
    /// pre-op + <see cref="Sunfish.Kernel.Audit.AuditEventType.IncidentOpened"/>
    /// post-op.
    /// </summary>
    ValueTask<IncidentRecord> OpenIncidentAsync(
        TenantId tenantId,
        Principal actor,
        string rootAlertId,
        string title,
        IReadOnlyList<string> runbookStepIds,
        CancellationToken ct = default);

    /// <summary>
    /// Close an incident with a resolution note. Requires
    /// <c>ShipAction.CloseIncident</c>. Emits
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.IncidentCloseRequested"/>
    /// pre-op + <see cref="Sunfish.Kernel.Audit.AuditEventType.IncidentClosed"/>
    /// post-op.
    /// </summary>
    ValueTask CloseIncidentAsync(
        TenantId tenantId,
        Principal actor,
        string incidentId,
        string resolutionNote,
        CancellationToken ct = default);
}

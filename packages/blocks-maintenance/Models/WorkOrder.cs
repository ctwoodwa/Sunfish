using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A formal instruction to a vendor to perform work, optionally tied to a
/// physical <see cref="EquipmentId"/> and a primary coordination
/// <c>ThreadId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>api-change-shape, MAJOR version bump per ADR 0053 A6</b> (W#19 Phase 5):
/// the previously-positional 10-arg constructor is replaced with init-only
/// properties; <c>RequestId</c> is dropped (the originating
/// <c>MaintenanceRequest</c> is recovered from the first
/// <c>WorkOrderCreated</c> audit record by Phase 5.1 — see
/// <c>WorkOrderListBlock.razor</c> + <c>MIGRATION.md</c>).
/// </para>
/// <para>
/// New optional fields per the Phase 5 schema migration: <see cref="EstimatedCost"/>
/// and <see cref="TotalCost"/> migrate to <see cref="Money"/> (per ADR 0051);
/// <see cref="Equipment"/> + <c>PrimaryThread</c> + <see cref="Appointment"/>
/// + <see cref="CompletionAttestation"/> + <see cref="EntryNotices"/> ride per
/// the Phase 3 child-entity surface; <see cref="AuditTrail"/> records the
/// emitted <c>AuditRecord.AuditId</c> trail per ADR 0049.
/// </para>
/// </remarks>
public sealed record WorkOrder
{
    /// <summary>Unique identifier for this work order.</summary>
    public required WorkOrderId Id { get; init; }

    /// <summary>Owning tenant (per <c>IMustHaveTenant</c>).</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The vendor assigned to perform the work.</summary>
    public required VendorId AssignedVendorId { get; init; }

    /// <summary>Current lifecycle status of this work order (13 states per ADR 0053 A4).</summary>
    public required WorkOrderStatus Status { get; init; }

    /// <summary>The date on which the work is scheduled to be performed.</summary>
    public required DateOnly ScheduledDate { get; init; }

    /// <summary>The date the work was completed, or <see langword="null"/> if not yet complete.</summary>
    public DateOnly? CompletedDate { get; init; }

    /// <summary>Estimated cost (currency-bound). <see langword="null"/> when not yet estimated. ADR 0051 will extend <see cref="Money"/> with banker's rounding + validation.</summary>
    public Money? EstimatedCost { get; init; }

    /// <summary>Total cost once invoiced/captured (replaces <c>ActualCost</c>); <see langword="null"/> until billed.</summary>
    public Money? TotalCost { get; init; }

    /// <summary>FK to the physical equipment this work targets, when applicable (per ADR 0053 A1; renamed from "Asset" per UPF Rule 4).</summary>
    public EquipmentId? Equipment { get; init; }

    // PrimaryThread (ThreadId? per ADR 0052) is added in W#19 Phase 6
    // (cross-package wiring). The W#20 Phase 1 contracts containing
    // ThreadId aren't on this branch's stack base; introducing the field
    // here would force a stack-wide rebase. Phase 6 wires it.

    /// <summary>Appointment slot bound to this work order (per W#19 Phase 3 child entity).</summary>
    public WorkOrderAppointment? Appointment { get; init; }

    /// <summary>Signature-bound completion attestation (per ADR 0054 + W#19 Phase 3).</summary>
    public WorkOrderCompletionAttestation? CompletionAttestation { get; init; }

    /// <summary>Right-of-entry notices recorded against this work order (per W#19 Phase 3).</summary>
    public IReadOnlyList<WorkOrderEntryNotice> EntryNotices { get; init; } = Array.Empty<WorkOrderEntryNotice>();

    /// <summary>Audit-record GUIDs (ADR 0049 <c>AuditRecord.AuditId</c>) emitted across this work order's lifecycle. The first id resolves to the originating <c>WorkOrderCreated</c> record whose payload body carries the source reference (replaces the dropped <c>RequestId</c> FK; W#19 Phase 5.1 wires <c>WorkOrderListBlock</c> to surface it).</summary>
    public IReadOnlyList<Guid> AuditTrail { get; init; } = Array.Empty<Guid>();

    /// <summary>Free-form notes from the vendor or property manager.</summary>
    public string? Notes { get; init; }

    /// <summary>The instant this record was first persisted.</summary>
    public required Instant CreatedAtUtc { get; init; }

    /// <summary>Last-update wall-clock time; bumped on every state-mutating operation.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

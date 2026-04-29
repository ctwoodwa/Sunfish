using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.Maintenance.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the 17 work-order lifecycle
/// events (ADR 0053 A8 + ADR 0049). Mirrors the
/// <c>TaxonomyAuditPayloadFactory</c> pattern from W#31. The caller signs
/// the payload via
/// <see cref="Sunfish.Foundation.Crypto.IOperationSigner"/> and constructs
/// the <see cref="AuditRecord"/>.
/// </summary>
internal static class WorkOrderAuditPayloadFactory
{
    private static AuditPayload Transition(WorkOrderId id, WorkOrderStatus previous, WorkOrderStatus next, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["work_order_id"] = id.Value,
            ["previous_status"] = previous.ToString(),
            ["new_status"] = next.ToString(),
            ["actor"] = actor.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.WorkOrderCreated"/>.</summary>
    public static AuditPayload Created(WorkOrderId id, WorkOrderStatus initial, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["work_order_id"] = id.Value,
            ["initial_status"] = initial.ToString(),
            ["actor"] = actor.Value,
        });

    /// <summary>Body for any work-order status-transition audit event.</summary>
    public static AuditPayload StatusTransition(WorkOrderId id, WorkOrderStatus previous, WorkOrderStatus next, ActorId actor) =>
        Transition(id, previous, next, actor);

    /// <summary>Body for <see cref="AuditEventType.WorkOrderEntryNoticeRecorded"/>.</summary>
    public static AuditPayload EntryNoticeRecorded(WorkOrderEntryNotice notice, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["entry_notice_id"] = notice.Id.Value,
            ["work_order_id"] = notice.WorkOrder.Value,
            ["planned_entry_utc"] = notice.PlannedEntryUtc.ToString("O"),
            ["entry_reason"] = notice.EntryReason,
            ["actor"] = actor.Value,
            ["notified_party_count"] = notice.NotifiedParties.Count,
        });

    /// <summary>Body for <see cref="AuditEventType.WorkOrderAppointmentScheduled"/>.</summary>
    public static AuditPayload AppointmentScheduled(WorkOrderAppointment appointment, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["appointment_id"] = appointment.Id.Value,
            ["work_order_id"] = appointment.WorkOrder.Value,
            ["slot_start_utc"] = appointment.SlotStartUtc.ToString("O"),
            ["slot_end_utc"] = appointment.SlotEndUtc.ToString("O"),
            ["actor"] = actor.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.WorkOrderAppointmentConfirmed"/>.</summary>
    public static AuditPayload AppointmentConfirmed(WorkOrderAppointment appointment, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["appointment_id"] = appointment.Id.Value,
            ["work_order_id"] = appointment.WorkOrder.Value,
            ["confirmed_by"] = actor.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.WorkOrderCompletionAttestationCaptured"/>.</summary>
    public static AuditPayload CompletionAttestationCaptured(WorkOrderCompletionAttestation attestation, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["attestation_id"] = attestation.Id.Value,
            ["work_order_id"] = attestation.WorkOrder.Value,
            ["signature_event_id"] = attestation.Signature.SignatureEventId,
            ["actor"] = actor.Value,
        });

    /// <summary>
    /// Maps a status transition (previous → next) to the matching
    /// <see cref="AuditEventType"/> per ADR 0053 A8.
    /// </summary>
    public static AuditEventType EventForTransition(WorkOrderStatus previous, WorkOrderStatus next) => (previous, next) switch
    {
        (WorkOrderStatus.Draft, WorkOrderStatus.Sent) => AuditEventType.WorkOrderSent,
        (WorkOrderStatus.Sent, WorkOrderStatus.Accepted) => AuditEventType.WorkOrderAccepted,
        (WorkOrderStatus.Accepted, WorkOrderStatus.Scheduled) => AuditEventType.WorkOrderScheduled,
        (WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress) => AuditEventType.WorkOrderStarted,
        (WorkOrderStatus.InProgress, WorkOrderStatus.OnHold) => AuditEventType.WorkOrderHeld,
        (WorkOrderStatus.OnHold, WorkOrderStatus.InProgress) => AuditEventType.WorkOrderResumed,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => AuditEventType.WorkOrderCompleted,
        (WorkOrderStatus.Completed, WorkOrderStatus.AwaitingSignOff) => AuditEventType.WorkOrderSignedOff,
        (_, WorkOrderStatus.Invoiced) => AuditEventType.WorkOrderInvoiced,
        (_, WorkOrderStatus.Paid) => AuditEventType.WorkOrderPaid,
        (_, WorkOrderStatus.Disputed) => AuditEventType.WorkOrderDisputed,
        (_, WorkOrderStatus.Closed) => AuditEventType.WorkOrderClosed,
        (_, WorkOrderStatus.Cancelled) => AuditEventType.WorkOrderCancelled,
        // AwaitingSignOff → OnHold has no dedicated AuditEventType per A8;
        // emit WorkOrderHeld for the on-hold side-branch.
        (WorkOrderStatus.AwaitingSignOff, WorkOrderStatus.OnHold) => AuditEventType.WorkOrderHeld,
        // Invoiced → OnHold side-branch — emit WorkOrderHeld.
        (WorkOrderStatus.Invoiced, WorkOrderStatus.OnHold) => AuditEventType.WorkOrderHeld,
        _ => throw new InvalidOperationException($"No AuditEventType mapped for transition {previous} → {next}."),
    };
}

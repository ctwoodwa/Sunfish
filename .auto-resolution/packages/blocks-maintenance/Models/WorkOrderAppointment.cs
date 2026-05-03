using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Appointment slot bound to a <see cref="WorkOrder"/>. CP-class per ADR 0053
/// amendment A9 — coordinated with the Flease lease primitive (per ADR 0028)
/// to prevent double-booking under partition. Phase 1 in-memory implementation
/// uses an in-process lock; Phase 2 will replace with Flease per ADR 0028.
/// </summary>
public sealed record WorkOrderAppointment
{
    /// <summary>Stable identifier for this appointment.</summary>
    public required WorkOrderAppointmentId Id { get; init; }

    /// <summary>The work order this appointment slots.</summary>
    public required WorkOrderId WorkOrder { get; init; }

    /// <summary>Wall-clock start of the appointment slot.</summary>
    public required DateTimeOffset SlotStartUtc { get; init; }

    /// <summary>Wall-clock end of the appointment slot.</summary>
    public required DateTimeOffset SlotEndUtc { get; init; }

    /// <summary>Lifecycle status — Proposed → Confirmed | Cancelled.</summary>
    public required AppointmentStatus Status { get; init; }

    /// <summary>Actor that proposed the appointment.</summary>
    public required ActorId ProposedBy { get; init; }

    /// <summary>Actor that confirmed; null while <see cref="Status"/> is <see cref="AppointmentStatus.Proposed"/>.</summary>
    public ActorId? ConfirmedBy { get; init; }

    /// <summary>Wall-clock time the appointment was confirmed; null while not yet confirmed.</summary>
    public DateTimeOffset? ConfirmedAt { get; init; }
}

/// <summary>Lifecycle status of a <see cref="WorkOrderAppointment"/>.</summary>
public enum AppointmentStatus
{
    /// <summary>Operator has proposed a slot; awaiting confirmation by the counter-party.</summary>
    Proposed,

    /// <summary>Both parties have confirmed; the slot is scheduled.</summary>
    Confirmed,

    /// <summary>Slot was cancelled before confirmation or revoked after.</summary>
    Cancelled
}

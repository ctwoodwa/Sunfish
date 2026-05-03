namespace Sunfish.Blocks.Scheduling.Models;

/// <summary>
/// Outcome of an <see cref="IScheduleReservationCoordinator.ReserveAsync"/>
/// call. Mirrors the shape of <c>Sunfish.Kernel.Ledger.PostingResult</c> so
/// the two CP-class writers in the kernel speak the same vocabulary.
/// </summary>
/// <param name="Success">
/// <c>true</c> iff the reservation was committed (or was a duplicate of a
/// previously-committed <see cref="SlotReservation.ReservationId"/>).
/// </param>
/// <param name="ReservationId">
/// The committed reservation's id. For a duplicate reservation id, this is
/// the id of the prior commit — never a new id.
/// </param>
/// <param name="RejectionReason">
/// Non-null iff <see cref="Success"/> is false. Canonical values:
/// <list type="bullet">
///   <item><description><c>QUORUM_UNAVAILABLE</c> — kernel-lease could not
///   reach a Flease quorum on the resource. Paper §6.3 fail-closed.</description></item>
///   <item><description><c>SLOT_INVERTED</c> — <see cref="SlotReservation.EndUtc"/>
///   is at or before <see cref="SlotReservation.StartUtc"/>.</description></item>
///   <item><description><c>SLOT_CONFLICT</c> — the slot overlaps an existing
///   reservation on the same resource (detected after the lease was held, so
///   no other node can race in between the check and the commit).</description></item>
/// </list>
/// </param>
/// <param name="HolderNodeId">
/// When <see cref="RejectionReason"/> is <c>QUORUM_UNAVAILABLE</c> and the
/// underlying lease coordinator surfaced a holder, the node id of the
/// current lease holder (so the UI can show "in use by node X"). <c>null</c>
/// otherwise.
/// </param>
public sealed record ReservationOutcome(
    bool Success,
    string ReservationId,
    string? RejectionReason = null,
    string? HolderNodeId = null);

using Sunfish.Blocks.Scheduling.Models;

namespace Sunfish.Blocks.Scheduling;

/// <summary>
/// CP-class write-coordination front door for the schedule view block.
/// Implements the paper §2.2 invariant: "Resource reservations, scheduled
/// slots → CP via distributed lease" by routing every reservation write
/// through a <see cref="Sunfish.Kernel.Lease.ILeaseCoordinator"/> grant
/// before letting it land.
/// </summary>
/// <remarks>
/// <para>
/// This is the "consumer site" wiring called out in the 2026-04-26
/// paper-vs-implementation drift audit (drift D6). The block previously
/// shipped only a UI view-switcher (<c>ScheduleViewBlock.razor</c>), which
/// meant the codebase contained the Flease coordinator but did not actually
/// use it for the slot-reservation record class the paper names. This
/// interface closes that gap.
/// </para>
/// <para>
/// <b>Why a coordinator and not a raw lease call at the UI layer?</b> The
/// reservation is a transactional unit (overlap check + commit) that must
/// happen atomically under a single lease grant. Surfacing
/// <c>ILeaseCoordinator</c> directly to UI code would invite consumers to
/// acquire-then-release without the overlap check, which is exactly the
/// double-booking failure mode the paper warns about. The coordinator
/// owns the read-then-write cycle and is the only legitimate write path
/// for slot reservations.
/// </para>
/// </remarks>
public interface IScheduleReservationCoordinator
{
    /// <summary>
    /// Acquire a lease on <see cref="SlotReservation.ResourceId"/>, check the
    /// requested slot does not overlap an existing reservation, and commit
    /// the reservation. Idempotent on
    /// <see cref="SlotReservation.ReservationId"/> — replaying the same
    /// reservation id returns the prior outcome without acquiring a new lease.
    /// </summary>
    /// <param name="reservation">The reservation to commit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ReservationOutcome"/>. <c>Success = false</c> with
    /// <c>RejectionReason = "QUORUM_UNAVAILABLE"</c> means the Flease
    /// coordinator could not reach a quorum and the write was blocked per
    /// paper §6.3. <c>Success = false</c> with <c>RejectionReason =
    /// "SLOT_CONFLICT"</c> means the lease was acquired but the slot was
    /// already taken (the holding lease was released between the read and
    /// the write — only possible across re-acquires, not within a single
    /// grant).
    /// </returns>
    Task<ReservationOutcome> ReserveAsync(SlotReservation reservation, CancellationToken ct);

    /// <summary>
    /// Cancel a previously-committed reservation. Acquires the same lease
    /// the reservation occupies, removes the slot, and releases. Returns
    /// <c>false</c> if no reservation with that id is on file.
    /// </summary>
    /// <param name="reservationId">Id of the reservation to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> CancelAsync(string reservationId, CancellationToken ct);

    /// <summary>
    /// Snapshot of currently-committed reservations for a single resource,
    /// ordered by start time. Read-only — does not acquire a lease. Use
    /// for UI rendering; never as a pre-write check (that path is owned by
    /// <see cref="ReserveAsync"/>).
    /// </summary>
    /// <param name="resourceId">Resource to query.</param>
    IReadOnlyList<SlotReservation> ListForResource(string resourceId);
}

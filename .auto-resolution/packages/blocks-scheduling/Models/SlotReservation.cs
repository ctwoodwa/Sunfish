namespace Sunfish.Blocks.Scheduling.Models;

/// <summary>
/// A request to reserve a single time-slot for a single resource. Paper §2.2
/// classifies "Resource reservations / scheduled slots" as CP — double-booking
/// is worse than unavailability — so reservation writes flow through
/// <see cref="IScheduleReservationCoordinator"/>, which acquires a Flease lease
/// (paper §6.3 / kernel-lease) before allowing the write to land.
/// </summary>
/// <param name="ReservationId">
/// Stable client-side identifier for this reservation. Used to detect
/// idempotent retries: two requests with the same <see cref="ReservationId"/>
/// are treated as one. Caller-controlled so client retries after a network
/// blip do not double-book.
/// </param>
/// <param name="ResourceId">
/// Stable identifier of the resource being reserved (room, equipment item,
/// staff member, vehicle…). Becomes part of the lease resource id, so all
/// reservations targeting the same resource serialize through the same lease.
/// </param>
/// <param name="StartUtc">Slot start (UTC).</param>
/// <param name="EndUtc">Slot end (UTC), exclusive. Must be strictly after
/// <paramref name="StartUtc"/>.</param>
/// <param name="HolderId">
/// Stable identifier of the principal that will own the reservation
/// (user id, account id, customer id…). Surfaces in audit/observability.
/// </param>
/// <param name="Description">
/// Optional free-form description (subject, purpose). Stored verbatim by
/// downstream persistence; the coordinator does not interpret it.
/// </param>
public sealed record SlotReservation(
    string ReservationId,
    string ResourceId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string HolderId,
    string? Description = null);

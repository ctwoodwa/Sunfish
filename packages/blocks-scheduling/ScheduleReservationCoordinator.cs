using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Blocks.Scheduling.Models;

using LeaseNs = Sunfish.Kernel.Lease;

namespace Sunfish.Blocks.Scheduling;

/// <summary>
/// Default <see cref="IScheduleReservationCoordinator"/> implementation. Wires
/// <see cref="LeaseNs.ILeaseCoordinator"/> (kernel-lease / Flease) into the
/// reservation write path so that "Resource reservations, scheduled slots →
/// CP via distributed lease" (paper §2.2) is enforced at the consumer site,
/// not just available in the kernel.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lease scope.</b> One lease per <see cref="SlotReservation.ResourceId"/>
/// (resource id <c>schedule:resource:{resourceId}</c>). Reservations on
/// disjoint resources can proceed in parallel; reservations on the same
/// resource serialize through the same lease. This matches paper §6.3's
/// "narrowest possible CP footprint" guidance — only contended resources
/// pay the coordination cost.
/// </para>
/// <para>
/// <b>Write path.</b>
/// <list type="number">
///   <item><description>Validate slot shape (<c>EndUtc &gt; StartUtc</c>).</description></item>
///   <item><description>Idempotency dedupe on
///   <see cref="SlotReservation.ReservationId"/>.</description></item>
///   <item><description>Acquire Flease lease on the resource. If
///   <see cref="LeaseNs.ILeaseCoordinator.AcquireAsync"/> returns
///   <c>null</c> (quorum unreachable), the write is blocked per paper
///   §6.3 and the call returns
///   <see cref="ReservationOutcome"/> with <c>QUORUM_UNAVAILABLE</c> —
///   we never silently fall through to a best-effort write.</description></item>
///   <item><description>Under the held lease, check the slot does not
///   overlap an existing reservation on the same resource.</description></item>
///   <item><description>Commit the reservation to the in-memory ledger and
///   release the lease in a <c>finally</c> block.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Persistence.</b> This coordinator stores reservations in-memory; that
/// is enough to test the lease-coordination contract and is the same
/// pattern <c>kernel-ledger.PostingEngine</c> uses for its in-process
/// projection. Concrete persistence (event log, EF DbContext, etc.) layers
/// on top via the <see cref="IScheduleReservationCoordinator"/> interface
/// and is intentionally out of scope for the D6 wiring fix.
/// </para>
/// </remarks>
public sealed class ScheduleReservationCoordinator : IScheduleReservationCoordinator
{
    /// <summary>Default lease duration used for slot-reservation serialization.</summary>
    internal static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(30);

    /// <summary>Lease resource-id prefix. Distinct from
    /// <c>ledger:account:</c> so the two CP-class writers can share the
    /// same Flease cluster without colliding.</summary>
    internal const string LeaseResourcePrefix = "schedule:resource:";

    private readonly LeaseNs.ILeaseCoordinator _leases;
    private readonly ILogger<ScheduleReservationCoordinator> _logger;

    // ReservationId → committed reservation (idempotency index).
    private readonly ConcurrentDictionary<string, SlotReservation> _byReservationId =
        new(StringComparer.Ordinal);

    // ResourceId → ordered list of committed reservations on that resource.
    // Mutations happen only under the resource lease, so the inner list does
    // not need its own lock.
    private readonly ConcurrentDictionary<string, List<SlotReservation>> _byResource =
        new(StringComparer.Ordinal);

    /// <summary>Constructs a new schedule reservation coordinator.</summary>
    /// <param name="leases">Distributed lease coordinator (kernel-lease).</param>
    /// <param name="logger">Optional logger.</param>
    public ScheduleReservationCoordinator(
        LeaseNs.ILeaseCoordinator leases,
        ILogger<ScheduleReservationCoordinator>? logger = null)
    {
        _leases = leases ?? throw new ArgumentNullException(nameof(leases));
        _logger = logger ?? NullLogger<ScheduleReservationCoordinator>.Instance;
    }

    /// <inheritdoc />
    public async Task<ReservationOutcome> ReserveAsync(SlotReservation reservation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentException.ThrowIfNullOrEmpty(reservation.ReservationId);
        ArgumentException.ThrowIfNullOrEmpty(reservation.ResourceId);
        ct.ThrowIfCancellationRequested();

        if (reservation.EndUtc <= reservation.StartUtc)
        {
            _logger.LogWarning(
                "Rejecting inverted reservation {ReservationId}: end {End} <= start {Start}",
                reservation.ReservationId, reservation.EndUtc, reservation.StartUtc);
            return new ReservationOutcome(false, reservation.ReservationId, "SLOT_INVERTED");
        }

        // Idempotency dedupe — replays of the same id return the prior outcome
        // without acquiring a fresh lease (matches PostingEngine.PostAsync).
        if (_byReservationId.TryGetValue(reservation.ReservationId, out _))
        {
            _logger.LogDebug(
                "ReservationId {ReservationId} already committed; returning prior result",
                reservation.ReservationId);
            return new ReservationOutcome(true, reservation.ReservationId);
        }

        var leaseResourceId = LeaseResourcePrefix + reservation.ResourceId;
        var lease = await _leases.AcquireAsync(leaseResourceId, DefaultLeaseDuration, ct)
            .ConfigureAwait(false);
        if (lease is null)
        {
            // Quorum unavailable. Paper §6.3: the CP-class write MUST block
            // and the UI MUST surface staleness. Catching this and proceeding
            // with the write is an architecture violation — see
            // QuorumUnavailableException xmldoc in kernel-lease.
            _logger.LogWarning(
                "Quorum unavailable for reservation {ReservationId} on resource {ResourceId}",
                reservation.ReservationId, reservation.ResourceId);
            return new ReservationOutcome(false, reservation.ReservationId, "QUORUM_UNAVAILABLE");
        }

        try
        {
            // Re-check idempotency under the lease in case another caller in
            // the same process committed the same id while we were waiting on
            // AcquireAsync. Belt-and-braces: AcquireAsync is per-resource, but
            // ReservationId is global, so two reservations against different
            // resources sharing one id could otherwise both commit.
            if (_byReservationId.ContainsKey(reservation.ReservationId))
            {
                return new ReservationOutcome(true, reservation.ReservationId);
            }

            var resourceList = _byResource.GetOrAdd(
                reservation.ResourceId,
                _ => new List<SlotReservation>());

            // Overlap check under the lease — no other node can have written
            // a conflicting slot in the window between our read and our
            // write because the lease is held cluster-wide.
            foreach (var existing in resourceList)
            {
                if (existing.StartUtc < reservation.EndUtc && reservation.StartUtc < existing.EndUtc)
                {
                    _logger.LogInformation(
                        "Slot conflict on resource {ResourceId}: requested {ReqStart}-{ReqEnd} overlaps {ExistingId} {ExistStart}-{ExistEnd}",
                        reservation.ResourceId,
                        reservation.StartUtc, reservation.EndUtc,
                        existing.ReservationId, existing.StartUtc, existing.EndUtc);
                    return new ReservationOutcome(false, reservation.ReservationId, "SLOT_CONFLICT");
                }
            }

            // Commit. Both indices are mutated only under the lease — safe.
            resourceList.Add(reservation);
            resourceList.Sort(static (a, b) => a.StartUtc.CompareTo(b.StartUtc));
            _byReservationId[reservation.ReservationId] = reservation;

            _logger.LogInformation(
                "Reserved slot {ReservationId} on {ResourceId} for {Holder} {Start}-{End}",
                reservation.ReservationId, reservation.ResourceId, reservation.HolderId,
                reservation.StartUtc, reservation.EndUtc);
            return new ReservationOutcome(true, reservation.ReservationId);
        }
        finally
        {
            try
            {
                await _leases.ReleaseAsync(lease, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Best-effort release — the lease auto-expires on the timer
                // even if our explicit release fails (paper §6.3 invariant).
                _logger.LogDebug(ex, "Swallowed error releasing lease {LeaseId}", lease.LeaseId);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(string reservationId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);
        ct.ThrowIfCancellationRequested();

        if (!_byReservationId.TryGetValue(reservationId, out var existing))
        {
            return false;
        }

        var leaseResourceId = LeaseResourcePrefix + existing.ResourceId;
        var lease = await _leases.AcquireAsync(leaseResourceId, DefaultLeaseDuration, ct)
            .ConfigureAwait(false);
        if (lease is null)
        {
            // Cancellation is also a CP-class write — block on quorum loss.
            _logger.LogWarning(
                "Quorum unavailable for cancellation {ReservationId} on resource {ResourceId}",
                reservationId, existing.ResourceId);
            return false;
        }

        try
        {
            if (!_byReservationId.TryRemove(reservationId, out var removed))
            {
                return false; // raced another canceller
            }

            if (_byResource.TryGetValue(removed.ResourceId, out var list))
            {
                list.RemoveAll(r => string.Equals(r.ReservationId, reservationId, StringComparison.Ordinal));
            }

            _logger.LogInformation(
                "Cancelled reservation {ReservationId} on {ResourceId}",
                reservationId, removed.ResourceId);
            return true;
        }
        finally
        {
            try
            {
                await _leases.ReleaseAsync(lease, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Swallowed error releasing lease {LeaseId}", lease.LeaseId);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SlotReservation> ListForResource(string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceId);
        if (!_byResource.TryGetValue(resourceId, out var list))
        {
            return Array.Empty<SlotReservation>();
        }
        // Snapshot — caller can iterate without worrying about concurrent
        // mutation. Mutations on the inner list happen only under a lease.
        return list.ToArray();
    }
}

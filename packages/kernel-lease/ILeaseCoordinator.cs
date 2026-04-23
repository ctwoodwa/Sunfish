namespace Sunfish.Kernel.Lease;

/// <summary>
/// Distributed lease coordinator for CP-class record writes. Paper §6.3 and
/// sync-daemon-protocol §6.
/// </summary>
/// <remarks>
/// <para>
/// A lease grants the holder exclusive write rights on a named resource for
/// a bounded window. The coordinator negotiates the grant with a
/// <see cref="LeaseCoordinatorOptions.QuorumSize"/>-sized subset of peers
/// (default <c>ceil(N/2)+1</c>). If quorum is unreachable the request
/// fails closed — <see cref="AcquireAsync"/> returns <c>null</c> rather than
/// falling back to a best-effort grant.
/// </para>
/// <para>
/// Leases auto-expire at <see cref="Lease.ExpiresAt"/>. A node that goes
/// offline therefore releases its leases at expiry without requiring a
/// release message to arrive. <see cref="ReleaseAsync"/> is the happy-path
/// explicit release for an online holder that wants to free the resource
/// early.
/// </para>
/// </remarks>
public interface ILeaseCoordinator : IAsyncDisposable
{
    /// <summary>
    /// Attempt to acquire a lease on <paramref name="resourceId"/> for
    /// <paramref name="duration"/>. Returns the granted <see cref="Lease"/>
    /// or <c>null</c> if quorum is unreachable (the caller must block the
    /// CP-class write and surface staleness per paper §13.2).
    /// </summary>
    /// <param name="resourceId">Stable identifier of the resource to lock.</param>
    /// <param name="duration">How long the lease should be valid once granted.</param>
    /// <param name="ct">Cancellation token for the proposal round.</param>
    Task<Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct);

    /// <summary>
    /// Release <paramref name="lease"/> ahead of expiry. Broadcasts
    /// <c>LEASE_RELEASE</c> to every known peer so they free the resource
    /// immediately rather than waiting for the expiry timer. Safe to call on
    /// a lease we no longer hold (idempotent).
    /// </summary>
    Task ReleaseAsync(Lease lease, CancellationToken ct);

    /// <summary>
    /// Check whether we currently hold an unexpired lease on
    /// <paramref name="resourceId"/>. Local-only check; does not round-trip
    /// to peers.
    /// </summary>
    bool Holds(string resourceId);

    /// <summary>All currently-held leases. May include leases that are past
    /// <see cref="Lease.ExpiresAt"/> but have not yet been pruned by the
    /// background expiry sweep — call <see cref="Holds"/> for a fresh view.</summary>
    IReadOnlyCollection<Lease> HeldLeases { get; }
}

/// <summary>
/// A granted distributed lease. Immutable — new instances are produced on
/// every successful acquire. Field semantics follow sync-daemon-protocol §6.
/// </summary>
/// <param name="LeaseId">Opaque unique id for the lease (hex of a 16-byte random value).</param>
/// <param name="ResourceId">The resource the lease protects.</param>
/// <param name="HolderNodeId">The node id of the holder (us, from our own perspective).</param>
/// <param name="AcquiredAt">UTC timestamp at which the grant was observed.</param>
/// <param name="ExpiresAt">UTC timestamp at which the lease becomes invalid.</param>
/// <param name="QuorumParticipants">
/// The peer endpoints whose <c>LEASE_GRANT</c> contributed to the quorum
/// for this lease. Used for release broadcasts and observability; a peer that
/// participated in grant should also see the release.
/// </param>
public sealed record Lease(
    string LeaseId,
    string ResourceId,
    string HolderNodeId,
    DateTimeOffset AcquiredAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> QuorumParticipants);

namespace Sunfish.Kernel.Lease;

/// <summary>
/// Tunables for <see cref="FleaseLeaseCoordinator"/>. All defaults match
/// paper §6.3 and sync-daemon-protocol §6.
/// </summary>
public sealed class LeaseCoordinatorOptions
{
    /// <summary>
    /// Default duration for a newly granted lease when the caller does not
    /// supply one at <see cref="ILeaseCoordinator.AcquireAsync"/> time.
    /// Paper §6.3 default: 30 seconds.
    /// </summary>
    public TimeSpan DefaultLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for <c>LEASE_GRANT</c> / <c>LEASE_DENIED</c>
    /// responses from peers before concluding that quorum is unreachable.
    /// Sync-daemon-protocol §6 default: 5 seconds.
    /// </summary>
    public TimeSpan ProposalTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Explicit quorum size. A value of <c>0</c> (the default) tells the
    /// coordinator to auto-compute <c>ceil(N/2)+1</c> from the current peer
    /// count at proposal time. Set this explicitly when you want a fixed
    /// quorum independent of membership churn.
    /// </summary>
    /// <remarks>
    /// <b>Single-node clusters:</b> when there are zero peers, auto-compute
    /// yields <c>1</c> — the local node trivially satisfies its own quorum
    /// and grants are issued without any wire traffic. Explicit
    /// <see cref="QuorumSize"/> values larger than <c>peers + 1</c> always
    /// fail; that is the intended fail-closed behaviour for undersized
    /// teams per paper §2.3.
    /// </remarks>
    public int QuorumSize { get; set; } = 0;

    /// <summary>
    /// How often the background sweep prunes expired leases from the
    /// responder's conflict cache. A lease that has passed
    /// <see cref="Lease.ExpiresAt"/> is treated as released even if the
    /// sweep has not yet run, so this is purely a memory-reclaim cadence.
    /// </summary>
    public TimeSpan ExpiryPruneInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum time <see cref="ILeaseCoordinator.ReleaseAsync"/> will wait
    /// for a peer to acknowledge that it has applied the
    /// <c>LEASE_RELEASE</c> (i.e. cleared the lease from its responder-side
    /// conflict cache). The acknowledgement is signalled by the peer
    /// closing its half of the connection after
    /// <see cref="FleaseLeaseCoordinator"/>'s responder loop runs
    /// <c>HandleLeaseRelease</c>; without this barrier, ReleaseAsync would
    /// return before peers had drained the release frame, allowing a
    /// follow-up acquire on the same resource (from any node) to race the
    /// drain and observe a stale "still held" response. A bounded timeout
    /// preserves the best-effort-release contract — a partitioned peer
    /// cannot stall release indefinitely; its lease entry will simply
    /// expire on the natural duration timer instead.
    /// </summary>
    public TimeSpan ReleaseAckTimeout { get; set; } = TimeSpan.FromSeconds(2);
}

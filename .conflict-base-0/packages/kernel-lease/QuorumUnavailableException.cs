namespace Sunfish.Kernel.Lease;

/// <summary>
/// Thrown by the exception-flavoured lease APIs when a lease proposal
/// cannot reach the configured quorum. Callers that prefer a null return
/// should use <see cref="ILeaseCoordinator.AcquireAsync"/> directly.
/// </summary>
/// <remarks>
/// Quorum-unavailable is the fail-closed state required by paper §6.3: the
/// CP-class write MUST block and the UI MUST surface staleness. Catching
/// this exception and proceeding with the write is an architecture
/// violation.
/// </remarks>
public sealed class QuorumUnavailableException : InvalidOperationException
{
    /// <summary>The resource whose lease proposal failed to reach quorum.</summary>
    public string ResourceId { get; }

    /// <summary>The quorum size that the proposal targeted.</summary>
    public int RequiredQuorum { get; }

    /// <summary>The number of <c>LEASE_GRANT</c> responses actually received.</summary>
    public int ObservedGrants { get; }

    public QuorumUnavailableException(string resourceId, int requiredQuorum, int observedGrants)
        : base($"Quorum unavailable for resource '{resourceId}': observed {observedGrants}/{requiredQuorum} grants.")
    {
        ResourceId = resourceId;
        RequiredQuorum = requiredQuorum;
        ObservedGrants = observedGrants;
    }
}

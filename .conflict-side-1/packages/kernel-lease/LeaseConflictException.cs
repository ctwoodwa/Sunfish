namespace Sunfish.Kernel.Lease;

/// <summary>
/// Thrown by the exception-flavoured lease APIs when a lease request is
/// denied because another node holds the resource. Callers that prefer a
/// null return should use <see cref="ILeaseCoordinator.AcquireAsync"/>
/// directly.
/// </summary>
public sealed class LeaseConflictException : InvalidOperationException
{
    /// <summary>The resource that was contested.</summary>
    public string ResourceId { get; }

    /// <summary>
    /// The node id of the current holder, if the denying peer supplied it.
    /// <c>null</c> when the denial did not identify the holder.
    /// </summary>
    public string? HeldBy { get; }

    public LeaseConflictException(string resourceId, string? heldBy)
        : base(BuildMessage(resourceId, heldBy))
    {
        ResourceId = resourceId;
        HeldBy = heldBy;
    }

    private static string BuildMessage(string resourceId, string? heldBy) =>
        heldBy is null
            ? $"Lease for resource '{resourceId}' is held by another node."
            : $"Lease for resource '{resourceId}' is held by node '{heldBy}'.";
}

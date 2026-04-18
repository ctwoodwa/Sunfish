namespace Sunfish.Federation.CapabilitySync;

/// <summary>
/// Outcome of a single <see cref="ICapabilitySyncer.ReconcileAsync"/> call.
/// </summary>
/// <param name="OpsTransferred">Number of new ops that were accepted and stored locally.</param>
/// <param name="OpsAlreadyPresent">Number of ops advertised by the peer that the local store already held.</param>
/// <param name="OpsRejected">Number of advertised ops that failed signature verification or were unavailable.</param>
/// <param name="UsedRibltFastPath">True iff the RIBLT fast path converged and drove the result set.</param>
/// <param name="UsedFullSetFallback">True iff the full-set fallback was used after RIBLT gave up.</param>
/// <param name="RejectedNonces">Nonces corresponding to ops that were rejected.</param>
public sealed record CapabilityReconcileResult(
    int OpsTransferred,
    int OpsAlreadyPresent,
    int OpsRejected,
    bool UsedRibltFastPath,
    bool UsedFullSetFallback,
    IReadOnlyList<Guid> RejectedNonces);

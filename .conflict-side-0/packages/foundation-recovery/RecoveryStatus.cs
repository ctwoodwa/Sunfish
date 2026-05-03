namespace Sunfish.Foundation.Recovery;

/// <summary>
/// State of an in-flight (or absent) recovery request — the coordinator's
/// public read model. Hosts query this to decide whether to surface
/// "owner is recovering" UI, route trustee-approval prompts, or trigger
/// the SQLCipher key rotation that follows
/// <see cref="RecoveryStatusKind.Completed"/>.
/// </summary>
public enum RecoveryStatusKind
{
    /// <summary>No recovery request is in flight.</summary>
    NoRequest = 0,

    /// <summary>
    /// A request has been initiated but quorum (per
    /// <see cref="RecoveryCoordinatorOptions.QuorumThreshold"/>) has not
    /// yet been reached.
    /// </summary>
    AwaitingAttestations = 1,

    /// <summary>
    /// Quorum reached; the <b>#48e</b> grace window is open. The original
    /// device may still dispute.
    /// </summary>
    GracePeriodActive = 2,

    /// <summary>
    /// Original device disputed during the grace window. The pending
    /// request was aborted; the new device's request is rejected and
    /// must be re-initiated if recovery is still required.
    /// </summary>
    Disputed = 3,

    /// <summary>
    /// Grace window expired without dispute. The host should now trigger
    /// the SQLCipher key rotation and broadcast the new device identity.
    /// </summary>
    Completed = 4,
}

/// <summary>
/// Snapshot of the coordinator's recovery state. Host UI reads this
/// to decide what to show (quorum progress, grace countdown, dispute
/// banner). Returned by <see cref="IRecoveryCoordinator.GetStatusAsync"/>.
/// </summary>
/// <param name="Kind">Top-level state.</param>
/// <param name="PendingRequest">
/// The active <see cref="RecoveryRequest"/>, or <c>null</c> when
/// <see cref="Kind"/> is <see cref="RecoveryStatusKind.NoRequest"/>.
/// </param>
/// <param name="AttestationsReceived">
/// Number of distinct trustee attestations received so far (deduped by
/// trustee NodeId).
/// </param>
/// <param name="QuorumThreshold">
/// Number of attestations required to start the grace window (mirrors
/// <see cref="RecoveryCoordinatorOptions.QuorumThreshold"/>).
/// </param>
/// <param name="GracePeriodStartedAt">
/// Instant the grace window began, or <c>null</c> if quorum has not yet
/// been reached.
/// </param>
/// <param name="GracePeriodElapsesAt">
/// Instant the grace window ends, or <c>null</c> if quorum has not yet
/// been reached. Equal to
/// <see cref="GracePeriodStartedAt"/> + <see cref="RecoveryCoordinatorOptions.GracePeriod"/>.
/// </param>
public sealed record RecoveryStatus(
    RecoveryStatusKind Kind,
    RecoveryRequest? PendingRequest,
    int AttestationsReceived,
    int QuorumThreshold,
    DateTimeOffset? GracePeriodStartedAt,
    DateTimeOffset? GracePeriodElapsesAt);

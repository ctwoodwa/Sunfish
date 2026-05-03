namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Persisted snapshot of the <see cref="RecoveryCoordinator"/>'s state
/// — the trustee set, the in-flight request (if any), accumulated
/// attestations, the grace-window timestamp, and the audit-chain tail.
/// </summary>
/// <remarks>
/// <para>
/// The coordinator hands a snapshot to <see cref="IRecoveryStateStore"/>
/// after every mutation so a host restart re-evaluates the same state
/// (per the Phase 1 plan's "7-day grace must survive device restart"
/// requirement).
/// </para>
/// <para>
/// The shape is deliberately flat — the dedicated audit-log substrate
/// forecast in <c>docs/specifications/inverted-stack-package-roadmap.md</c>
/// will subsume the chain pointer in a follow-up; until then the
/// coordinator carries it directly.
/// </para>
/// </remarks>
public sealed class RecoveryCoordinatorState
{
    /// <summary>
    /// The set of currently-designated trustees, keyed by NodeId. Bounded
    /// by <see cref="RecoveryCoordinatorOptions.MaxTrustees"/>.
    /// </summary>
    public IReadOnlyDictionary<string, TrusteeDesignation> Trustees { get; init; }
        = new Dictionary<string, TrusteeDesignation>(StringComparer.Ordinal);

    /// <summary>
    /// The in-flight recovery request, or <c>null</c> if no request has
    /// been initiated. Cleared by a successful dispute (state stays as
    /// <see cref="RecoveryStatusKind.Disputed"/> until the next initiation).
    /// </summary>
    public RecoveryRequest? PendingRequest { get; init; }

    /// <summary>
    /// Attestations received against <see cref="PendingRequest"/>, keyed
    /// by trustee NodeId so duplicate submissions from the same trustee
    /// don't double-count toward quorum.
    /// </summary>
    public IReadOnlyDictionary<string, TrusteeAttestation> Attestations { get; init; }
        = new Dictionary<string, TrusteeAttestation>(StringComparer.Ordinal);

    /// <summary>
    /// Instant the grace window began, or <c>null</c> if quorum has not
    /// been reached.
    /// </summary>
    public DateTimeOffset? GracePeriodStartedAt { get; init; }

    /// <summary><c>true</c> after a valid dispute landed during the grace window.</summary>
    public bool Disputed { get; init; }

    /// <summary><c>true</c> after the grace window expired without a dispute and the coordinator emitted <see cref="RecoveryEventType.RecoveryCompleted"/>.</summary>
    public bool Completed { get; init; }

    /// <summary>
    /// SHA-256 of the most recent <see cref="RecoveryEvent"/> emitted, or
    /// <c>null</c> before the first event. Threaded into the next event's
    /// <see cref="RecoveryEvent.PreviousEventHash"/> so the audit chain
    /// is replay-verifiable.
    /// </summary>
    public byte[]? LastEventHash { get; init; }

    /// <summary>An empty initial state (no trustees, no request).</summary>
    public static RecoveryCoordinatorState Empty { get; } = new RecoveryCoordinatorState();
}

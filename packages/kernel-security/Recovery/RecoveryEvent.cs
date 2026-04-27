namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// Phase 1 G6 sub-pattern <b>#48f (signed audit)</b> per ADR 0046 — an
/// audit-log entry recording a step in the recovery workflow. Written
/// to the per-tenant audit log for forensic visibility into who recovered
/// keys, when, with which trustees' attestations, and what disputes
/// (if any) were raised during the grace window.
/// </summary>
/// <remarks>
/// <para>
/// Recovery events are append-only and intended to be hash-chained
/// (each event includes the hash of the previous event in its
/// <see cref="PreviousEventHash"/> field) so a tamper after the fact
/// is detectable by replaying the chain. The orchestrator populates
/// the chain pointer; this record's contract is the structural
/// envelope.
/// </para>
/// <para>
/// <b>What this is NOT.</b> This is the data record for an audit-log
/// entry. It does not implement the audit-log substrate (durable storage,
/// chain verification, query API) — that's the
/// <c>Sunfish.Kernel.Audit</c> follow-up forecast in
/// <c>docs/specifications/inverted-stack-package-roadmap.md</c>. Phase 1
/// emits these records via the <c>RecoveryCoordinator</c> implementation;
/// they're persisted alongside the existing kernel-ledger entries until
/// the dedicated audit substrate ships.
/// </para>
/// </remarks>
public sealed record RecoveryEvent(
    RecoveryEventType Type,
    string ActorNodeId,
    string TargetNodeId,
    DateTimeOffset OccurredAt,
    byte[]? PreviousEventHash,
    IReadOnlyDictionary<string, string> Detail);

/// <summary>
/// The classification of a <see cref="RecoveryEvent"/>. Each step in the
/// trustee-recovery and paper-key-recovery workflows produces one entry
/// of the corresponding type.
/// </summary>
public enum RecoveryEventType
{
    /// <summary>
    /// The owner designated a trustee. <c>Detail</c> includes the trustee's
    /// NodeId; one event per trustee added.
    /// </summary>
    TrusteeDesignated = 1,

    /// <summary>
    /// A trustee was removed from the trustee set. Replays of an old
    /// attestation from this trustee are dropped after this event lands.
    /// </summary>
    TrusteeRevoked = 2,

    /// <summary>
    /// A new device sent a <see cref="RecoveryRequest"/>. <c>ActorNodeId</c>
    /// is the new device's claimed NodeId.
    /// </summary>
    RecoveryInitiated = 3,

    /// <summary>
    /// A trustee returned a <see cref="TrusteeAttestation"/>. Multiple
    /// events accumulate until quorum.
    /// </summary>
    AttestationReceived = 4,

    /// <summary>
    /// Quorum (3 of 5 per ADR 0046) reached; the 7-day grace window
    /// (sub-pattern #48e) starts.
    /// </summary>
    GracePeriodStarted = 5,

    /// <summary>
    /// The original device disputed the recovery during the grace window.
    /// Coordinator aborts the recovery; new device's request is rejected.
    /// </summary>
    RecoveryDisputed = 6,

    /// <summary>
    /// Grace window expired without dispute; new device finalizes
    /// recovery (re-encrypts SQLCipher, broadcasts new identity).
    /// </summary>
    RecoveryCompleted = 7,

    /// <summary>
    /// Owner used the BIP-39 paper key (sub-pattern #48c) to recover.
    /// Skips the trustee/grace path entirely. <c>ActorNodeId</c> is the
    /// recovering device.
    /// </summary>
    PaperKeyRecoveryUsed = 8,
}

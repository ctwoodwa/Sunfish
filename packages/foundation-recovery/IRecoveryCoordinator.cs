namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Orchestrates the multi-sig social recovery flow per ADR 0046 sub-patterns
/// <b>#48a (multi-sig)</b>, <b>#48e (timed grace)</b>, and <b>#48f (signed audit)</b>.
/// </summary>
/// <remarks>
/// <para>
/// The coordinator owns the state machine that drives a recovery
/// request from initiation, through trustee attestation and quorum,
/// through the 7-day grace window, to either dispute or completion.
/// Every transition produces a <see cref="RecoveryEvent"/> that the host
/// is responsible for persisting / replicating to the per-tenant audit
/// log (sub-pattern #48f).
/// </para>
/// <para>
/// <b>Thread model.</b> Methods serialize internally; concurrent calls
/// are safe but processed in submission order. Long-running I/O happens
/// only in the <see cref="IRecoveryStateStore"/>.
/// </para>
/// <para>
/// <b>Restart safety.</b> The coordinator persists state after every
/// mutation, so process restart resumes the in-flight request with the
/// same trustee set, attestations, and grace-window timestamp. The host
/// should call <see cref="EvaluateGracePeriodAsync"/> on startup to
/// pick up any window that elapsed while the process was down.
/// </para>
/// <para>
/// <b>What this does NOT do.</b> The coordinator does not re-encrypt
/// SQLCipher with a new key, does not broadcast events to peers, and
/// does not own the audit-log substrate. It surfaces events; the host
/// composes them with sync, persistence, and key-rotation primitives.
/// </para>
/// </remarks>
public interface IRecoveryCoordinator
{
    /// <summary>
    /// Add <paramref name="trusteeNodeId"/> to the owner's designated
    /// trustee set per sub-pattern #48a. Bounded by
    /// <see cref="RecoveryCoordinatorOptions.MaxTrustees"/>.
    /// <paramref name="trusteeDHPublicKey"/> (W#67 PR 5 binding) is
    /// captured at designation time and cross-checked against every
    /// later <see cref="TrusteeAttestation.TrusteeDHPublicKey"/> via
    /// <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>
    /// — attestations with a different DH key are silently dropped.
    /// </summary>
    /// <returns>The emitted <see cref="RecoveryEventType.TrusteeDesignated"/> event.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the trustee set is already at <see cref="RecoveryCoordinatorOptions.MaxTrustees"/>,
    /// or the same NodeId is already designated.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// If <paramref name="trusteeDHPublicKey"/> is not exactly
    /// <see cref="TrusteeDesignation.DHPublicKeyLength"/> bytes.
    /// </exception>
    Task<RecoveryEvent> DesignateTrusteeAsync(
        string trusteeNodeId,
        ReadOnlyMemory<byte> trusteePublicKey,
        ReadOnlyMemory<byte> trusteeDHPublicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove <paramref name="trusteeNodeId"/> from the designated set.
    /// Replays of attestations from this trustee against any subsequent
    /// request are silently dropped after the revocation lands.
    /// </summary>
    /// <returns>The emitted <see cref="RecoveryEventType.TrusteeRevoked"/> event.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the trustee is not currently designated.
    /// </exception>
    Task<RecoveryEvent> RevokeTrusteeAsync(
        string trusteeNodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a new recovery request. The coordinator validates the
    /// request's signature against the embedded ephemeral public key
    /// and rejects it if any earlier request is still in-flight (to
    /// disambiguate concurrent recoveries; the host can reset state
    /// by completing or disputing the prior request first).
    /// </summary>
    /// <returns>The emitted <see cref="RecoveryEventType.RecoveryInitiated"/> event.</returns>
    /// <exception cref="ArgumentException">If the request signature is invalid.</exception>
    /// <exception cref="InvalidOperationException">If a prior request is still active.</exception>
    Task<RecoveryEvent> InitiateRecoveryAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a trustee's attestation. The coordinator verifies the
    /// attestation's signature, that its
    /// <see cref="TrusteeAttestation.RecoveryRequestHash"/> matches the
    /// pending request, and that the attesting NodeId is in the
    /// designated trustee set. Attestations failing any of these checks
    /// (or duplicates from a trustee that has already attested) are
    /// silently dropped — the returned event is <c>null</c> in those cases.
    /// </summary>
    /// <returns>
    /// The emitted <see cref="RecoveryEventType.AttestationReceived"/> event,
    /// followed by <see cref="RecoveryEventType.GracePeriodStarted"/> if
    /// the attestation crossed the quorum threshold. Returns <c>null</c>
    /// when the attestation was dropped (unknown trustee, hash mismatch,
    /// duplicate). The grace-period event, when it fires, is the second
    /// element of <see cref="RecoveryAttestationOutcome.Events"/>.
    /// </returns>
    Task<RecoveryAttestationOutcome> SubmitAttestationAsync(
        TrusteeAttestation attestation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispute the pending recovery during the grace window. The
    /// coordinator validates the dispute's signature, that its
    /// <see cref="RecoveryDispute.RecoveryRequestHash"/> matches the
    /// pending request, and that <see cref="RecoveryDispute.DisputingPublicKey"/>
    /// is in the configured disputer set. Disputes outside the grace
    /// window or against a missing request are rejected with
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>The emitted <see cref="RecoveryEventType.RecoveryDisputed"/> event.</returns>
    Task<RecoveryEvent> DisputeRecoveryAsync(
        RecoveryDispute dispute,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// W#67 / ADR 0046-A6 — record a trustee's encrypted root-seed
    /// envelope at trustee-designation time. The owner's
    /// <c>TrusteeSetupPage</c> encrypts the install root seed with
    /// <c>IX25519KeyAgreement.Box</c> against the trustee's per-team
    /// X25519 public key and calls this method to persist the resulting
    /// ciphertext + nonce + sender ephemeral public key. The trustee's
    /// local Anchor reads its own entry during the approval flow to
    /// re-encrypt the seed toward the recovering device's ephemeral key.
    /// Idempotent — re-calling with the same NodeId overwrites the prior
    /// envelope (use when the owner re-runs setup after key rotation).
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="encryptedSeed"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="trusteeNodeId"/> is empty or differs from <see cref="TrusteeEncryptedSeed.TrusteeNodeId"/>.</exception>
    Task SetupTrusteeAsync(
        string trusteeNodeId,
        TrusteeEncryptedSeed encryptedSeed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-evaluate the grace window. If quorum is reached, the window
    /// has elapsed, and no dispute landed, the coordinator emits
    /// <see cref="RecoveryEventType.RecoveryCompleted"/> and transitions
    /// to <see cref="RecoveryStatusKind.Completed"/>. Hosts should call
    /// this on startup and on any periodic tick (e.g., every 60 seconds).
    /// </summary>
    /// <returns>
    /// A <see cref="RecoveryCompletionResult"/> carrying the emitted
    /// <see cref="RecoveryEventType.RecoveryCompleted"/> event and the
    /// trustee attestations that contributed to quorum (so the host's
    /// completion handler can decrypt the seed envelopes per
    /// ADR 0046-A6); <c>null</c> if no transition was warranted (no
    /// request, quorum not reached, grace not elapsed, already
    /// completed/disputed). W#67 PR 3 — return type widened from
    /// <c>RecoveryEvent?</c> to retain the attestations.
    /// </returns>
    Task<RecoveryCompletionResult?> EvaluateGracePeriodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshot of the coordinator's state for host UI consumption.
    /// </summary>
    Task<RecoveryStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// W#67 PR 5 — fetch the persisted <see cref="TrusteeEncryptedSeed"/>
    /// for <paramref name="trusteeNodeId"/>, written previously via
    /// <see cref="SetupTrusteeAsync"/>. The trustee's local Anchor reads
    /// its own envelope during the approval flow to OpenBox the
    /// root seed before re-encrypting it toward the recovering device's
    /// ephemeral DH key.
    /// </summary>
    /// <returns>
    /// The envelope, or <c>null</c> if no setup record exists for this
    /// trustee NodeId (owner never designated me, or the setup record
    /// was wiped).
    /// </returns>
    Task<TrusteeEncryptedSeed?> GetTrusteeEncryptedSeedAsync(
        string trusteeNodeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of <see cref="IRecoveryCoordinator.SubmitAttestationAsync"/> —
/// the events that fired (zero, one, or two) and a flag indicating
/// whether the attestation was accepted at all.
/// </summary>
/// <param name="Accepted">
/// <c>false</c> if the attestation was silently dropped (unknown trustee,
/// hash mismatch, duplicate, no pending request, recovery already
/// completed or disputed). <c>true</c> when at least the
/// <see cref="RecoveryEventType.AttestationReceived"/> event fired.
/// </param>
/// <param name="Events">
/// Up to two events: the <see cref="RecoveryEventType.AttestationReceived"/>
/// event always when <see cref="Accepted"/> is <c>true</c>, plus a
/// <see cref="RecoveryEventType.GracePeriodStarted"/> event if quorum
/// was reached on this submission.
/// </param>
public sealed record RecoveryAttestationOutcome(
    bool Accepted,
    IReadOnlyList<RecoveryEvent> Events);

/// <summary>
/// Pluggable validator that decides whether a public key presented in a
/// <see cref="RecoveryDispute"/> belongs to an identity authorized to
/// abort an in-flight recovery. Hosts wire this against their existing
/// owner-identity primitives (for Anchor, the NodeIdentity public keys
/// of the owner's still-functional devices).
/// </summary>
/// <remarks>
/// Phase 1 ships <see cref="FixedDisputerValidator"/> for tests and for
/// hosts that have a single owner-device public key at coordinator
/// construction. Multi-device owner support arrives in Phase 1.x via a
/// dynamic implementation that queries the keystore.
/// </remarks>
public interface IDisputerValidator
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="disputerPublicKey"/>
    /// belongs to an identity authorized to dispute. Implementations
    /// must use a constant-time comparison when checking against known
    /// keys.
    /// </summary>
    Task<bool> IsAuthorizedAsync(
        ReadOnlyMemory<byte> disputerPublicKey,
        CancellationToken cancellationToken = default);
}

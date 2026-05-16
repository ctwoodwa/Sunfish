namespace Sunfish.Foundation.Recovery;

/// <summary>
/// W#67 / ADR 0046-A6 — outcome of
/// <see cref="IRecoveryCoordinator.EvaluateGracePeriodAsync"/> when the
/// grace window elapses without dispute. Replaces the prior
/// <c>Task&lt;RecoveryEvent?&gt;</c> return so callers
/// (<c>AnchorRecoveryCompletionHandler</c>) can retrieve the trustee
/// attestations needed to decrypt the seed envelopes alongside the
/// <see cref="RecoveryEvent"/> they need to persist.
/// </summary>
/// <param name="Event">The emitted <see cref="RecoveryEventType.RecoveryCompleted"/> event.</param>
/// <param name="Attestations">
/// All trustee attestations that contributed to the completion. Carries
/// each trustee's encrypted seed envelope; the completion handler
/// decrypts these with the recovering device's ephemeral X25519 private
/// key to recover the root seed.
/// </param>
public sealed record RecoveryCompletionResult(
    RecoveryEvent Event,
    IReadOnlyList<TrusteeAttestation> Attestations);

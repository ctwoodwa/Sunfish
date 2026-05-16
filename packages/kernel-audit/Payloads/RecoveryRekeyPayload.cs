namespace Sunfish.Kernel.Audit.Payloads;

/// <summary>
/// W#67 / ADR 0046-A6 — audit payload emitted by
/// <c>Sunfish.Anchor.Services.AnchorRecoveryCompletionHandler</c> after
/// the social-recovery seed-delivery protocol completes. Records the
/// quorum-side counts + the rekey outcome so post-recovery audit
/// review can distinguish a clean rekey from a divergent-seed abort
/// or a min-decryption-failed abort without re-deriving from the
/// underlying CRDT events.
/// </summary>
/// <param name="TargetNodeId">The recovering device's NodeId.</param>
/// <param name="CompletedAt">Wall-clock instant the handler returned.</param>
/// <param name="AttestationCount">
/// Total attestations the coordinator surfaced to the handler.
/// </param>
/// <param name="SuccessfulDecryptions">
/// How many attestation envelopes the handler successfully OpenBoxed.
/// Will be ≤ <see cref="AttestationCount"/>.
/// </param>
/// <param name="ReKeySucceeded">
/// <c>true</c> when <c>IEncryptedStore.RotateKeyAsync</c> committed
/// against the active team. <c>false</c> when the handler aborted at
/// any earlier stage (missing ephemeral key, zero decryptions,
/// divergent seeds, no active team).
/// </param>
/// <param name="FailureReason">
/// <c>null</c> on success; otherwise one of the documented abort
/// reasons: <c>"missing_ephemeral_key"</c>,
/// <c>"min_decryption_failed"</c>, <c>"divergent_seeds"</c>,
/// <c>"multi_team_unsupported"</c>, <c>"no_active_team"</c>,
/// <c>"malformed_envelope"</c>.
/// </param>
public sealed record RecoveryRekeyPayload(
    string TargetNodeId,
    DateTimeOffset CompletedAt,
    int AttestationCount,
    int SuccessfulDecryptions,
    bool ReKeySucceeded,
    string? FailureReason);

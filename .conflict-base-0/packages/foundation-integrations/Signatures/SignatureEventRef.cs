namespace Sunfish.Foundation.Integrations.Signatures;

/// <summary>
/// Opaque reference to a captured signature event. W#19 Phase 0 introduces
/// this stub mirroring the addendum's Money/ThreadId pattern (PR #274) so
/// W#19 Phase 3's <c>WorkOrderCompletionAttestation</c> child entity can
/// compile. ADR 0054 Stage 06 (kernel-signatures) will extend with the full
/// signature-capture semantics; this stub is just the FK shape consumers
/// store.
/// </summary>
/// <param name="SignatureEventId">Underlying signature-event identifier.</param>
public readonly record struct SignatureEventRef(Guid SignatureEventId);

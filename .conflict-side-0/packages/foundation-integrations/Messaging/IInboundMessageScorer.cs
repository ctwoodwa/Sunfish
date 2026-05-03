namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Plug-in contract for scoring inbound messages — Layer 4 of the 5-layer
/// defense per ADR 0052 amendment A1. Default implementation in W#20
/// Phase 5 returns 0 (NullScorer); future plug-ins may consult spam
/// classifiers, abuse heuristics, or public-listings inquiry-form posture.
/// </summary>
public interface IInboundMessageScorer
{
    /// <summary>
    /// Scores an inbound envelope. Higher scores indicate higher abuse risk.
    /// The 5-layer defense pipeline thresholds configure when to soft-reject
    /// (queue for unrouted triage) vs hard-reject (drop with audit emission).
    /// </summary>
    /// <param name="envelope">Inbound envelope after Layers 1–3 (provider sig + sender allow-list + rate limit) have passed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An abuse score, where 0 means "no signal" and 100 means "definitely abuse."</returns>
    Task<int> ScoreAsync(InboundMessageEnvelope envelope, CancellationToken ct);
}

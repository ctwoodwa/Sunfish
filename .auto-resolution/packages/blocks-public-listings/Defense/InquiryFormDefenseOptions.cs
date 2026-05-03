namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Tuning knobs for the W#28 5-layer defense pipeline (Phase 5b
/// thresholds per ADR 0059). Scores are 0–100 per
/// <see cref="Sunfish.Foundation.Integrations.Messaging.IInboundMessageScorer"/>.
/// </summary>
public sealed record InquiryFormDefenseOptions
{
    /// <summary>
    /// Score &gt;= this value at Layer 4 → hard-reject + audit (the
    /// inquiry is dropped and never queued for triage). Defaults to 80.
    /// </summary>
    public int HardRejectScore { get; init; } = 80;

    /// <summary>
    /// Score &gt;= this value at Layer 5 (and below
    /// <see cref="HardRejectScore"/>) → enqueue for manual triage +
    /// audit + soft-reject (no automatic forward to the leasing
    /// pipeline). Defaults to 50.
    /// </summary>
    public int SoftRejectScore { get; init; } = 50;
}

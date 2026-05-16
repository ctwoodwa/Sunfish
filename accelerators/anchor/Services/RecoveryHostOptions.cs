namespace Sunfish.Anchor.Services;

/// <summary>
/// Options for the W#63 recovery-host pipeline.
/// Section name: <c>Anchor:Recovery</c>.
/// </summary>
public sealed class RecoveryHostOptions
{
    /// <summary>The configuration section that binds to this options class.</summary>
    public const string SectionName = "Anchor:Recovery";

    /// <summary>
    /// How often the polling service ticks <see cref="Sunfish.Foundation.Recovery.IRecoveryCoordinator.EvaluateGracePeriodAsync"/>.
    /// Defaults to 60 seconds — the grace window is 7 days per ADR 0046, so
    /// sub-minute polling wastes cycles. Per XO ruling 2026-05-16 §(c).
    /// </summary>
    public int GracePollIntervalSeconds { get; set; } = 60;
}

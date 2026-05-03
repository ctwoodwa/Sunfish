using System;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// ±5-minute clock-skew replay-attack window per ADR 0031-A1.2.
/// Anchor rejects events whose <see cref="BridgeSubscriptionEvent.EffectiveAt"/>
/// differs from receive-time by more than the configured tolerance.
/// Default matches AWS Signature V4 + GitHub webhook conventions
/// (5 minutes); tunable per-deployment.
/// </summary>
public sealed class ReplayWindow
{
    /// <summary>Default ±5 minutes per A1.2.</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _tolerance;

    public ReplayWindow(TimeSpan? tolerance = null)
    {
        _tolerance = tolerance ?? DefaultTolerance;
    }

    /// <summary>True iff <paramref name="effectiveAt"/> is within the configured window of <paramref name="receivedAt"/>.</summary>
    public bool IsFresh(DateTimeOffset effectiveAt, DateTimeOffset receivedAt)
    {
        var skew = receivedAt - effectiveAt;
        if (skew < TimeSpan.Zero) skew = -skew; // |Δt|
        return skew <= _tolerance;
    }

    /// <summary>The signed clock-skew in seconds (effective − received). Positive when effectiveAt is in the future.</summary>
    public double SkewSeconds(DateTimeOffset effectiveAt, DateTimeOffset receivedAt) =>
        (effectiveAt - receivedAt).TotalSeconds;
}

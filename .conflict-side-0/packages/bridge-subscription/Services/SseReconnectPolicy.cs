using System;
using System.Collections.Generic;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// SSE reconnect schedule per ADR 0031-A1.12.4: <c>1s → 5s → 30s →
/// 60s capped</c>; <b>UNBOUNDED</b> retry count. Distinct from the
/// 7-attempt webhook retry policy — SSE never dead-letters the
/// connection. Per the W#36 hand-off halt-condition #2, conflating SSE
/// reconnect with webhook retry is the most common drift; this policy
/// is intentionally a separate type.
/// </summary>
public static class SseReconnectPolicy
{
    /// <summary>The 4 distinct delays before the 60s ceiling applies.</summary>
    public static readonly IReadOnlyList<TimeSpan> Delays = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    };

    /// <summary>
    /// Returns the wait time before reconnect attempt
    /// <paramref name="attempt"/> (1-indexed). Attempt 1 returns the
    /// first 1-second delay; attempts &gt; 4 return the 60-second cap.
    /// Throws on zero/negative.
    /// </summary>
    public static TimeSpan DelayBeforeAttempt(int attempt)
    {
        if (attempt < 1) throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempt must be ≥ 1.");
        if (attempt <= Delays.Count) return Delays[attempt - 1];
        return Delays[^1]; // capped
    }
}

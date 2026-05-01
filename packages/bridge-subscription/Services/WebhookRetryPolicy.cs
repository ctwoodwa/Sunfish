using System;
using System.Collections.Generic;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// The 7-attempt exponential-backoff schedule for Bridge webhook
/// retries per ADR 0031-A1.5: <c>1s → 5s → 30s → 5min → 30min → 2h →
/// 12h</c>. The 1st delivery attempt is immediate; subsequent attempts
/// wait the corresponding delay. After the 7th retry exhausts, the
/// event moves to the dead-letter queue + emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.BridgeSubscriptionEventDeliveryFailedTerminal"/>.
/// </summary>
public static class WebhookRetryPolicy
{
    /// <summary>The 7 delays, in order, per A1.5.</summary>
    public static readonly IReadOnlyList<TimeSpan> Delays = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    };

    /// <summary>The maximum number of delivery attempts including the first.</summary>
    public static readonly int MaxAttempts = Delays.Count + 1; // 1 initial + 7 retries = 8

    /// <summary>
    /// Returns the wait time before <paramref name="attempt"/> (1-indexed:
    /// attempt 1 returns <see cref="TimeSpan.Zero"/>; attempt 2 returns the
    /// 1-second delay; attempt 8 returns the 12-hour delay; attempt 9+ throws).
    /// </summary>
    public static TimeSpan DelayBeforeAttempt(int attempt)
    {
        if (attempt < 1) throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempt must be ≥ 1.");
        if (attempt == 1) return TimeSpan.Zero;
        if (attempt > MaxAttempts) throw new ArgumentOutOfRangeException(nameof(attempt), attempt, $"Attempt must be ≤ {MaxAttempts}.");
        return Delays[attempt - 2];
    }

    /// <summary>True when <paramref name="attempt"/> is the last allowed attempt.</summary>
    public static bool IsTerminalAttempt(int attempt) => attempt >= MaxAttempts;
}

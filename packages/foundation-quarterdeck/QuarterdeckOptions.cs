using System;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Host-configurable Quarterdeck tunables per ADR 0080 §2.3 + §5.1.
/// Heartbeat cadence + provider/source timeouts govern subscription
/// liveness + per-source aggregation budget.
/// </summary>
public sealed class QuarterdeckOptions
{
    /// <summary>
    /// Cadence at which
    /// <see cref="IQuarterdeckDataProvider.SubscribeSnapshotAsync"/>
    /// emits even when no underlying state changed. Default 30s; lower
    /// values increase subscriber CPU + recompose pressure without
    /// proportional UX gain.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Total budget for assembling one
    /// <see cref="QuarterdeckSnapshot"/>. When exceeded, the data
    /// provider surfaces partial results — readers MUST treat absent
    /// fields as <see cref="DepartmentStatus.Unknown"/> rather than
    /// denied. Default 10s.
    /// </summary>
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Per-source aggregation budget. Slow alert/KPI sources are
    /// dropped past this deadline; their absence is logged + surfaced
    /// as <see cref="DepartmentStatus.Unknown"/> for downstream cards.
    /// Default 5s.
    /// </summary>
    public TimeSpan PerSourceTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Canonical defaults per ADR 0080 §2.3:
    /// <c>HeartbeatInterval = 30s</c>, <c>ProviderTimeout = 10s</c>,
    /// <c>PerSourceTimeout = 5s</c>. Returns a fresh instance — callers
    /// MAY mutate the returned instance without affecting other
    /// callers.
    /// </summary>
    public static QuarterdeckOptions Default => new();
}

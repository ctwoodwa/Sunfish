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
    /// emits even when no underlying state changed. Default 30s per
    /// ADR 0080 §1; lower values increase subscriber CPU + recompose
    /// pressure without proportional UX gain.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Total budget for assembling one
    /// <see cref="QuarterdeckSnapshot"/>. When exceeded, the data
    /// provider surfaces partial results — readers MUST treat absent
    /// fields as <see cref="DepartmentStatus.Unknown"/> rather than
    /// denied. Default 2s per ADR 0080 §1 (1s aggregate target +
    /// defense-in-depth headroom).
    /// </summary>
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Per-source aggregation budget. Slow alert/KPI sources are
    /// dropped past this deadline; their absence is logged + surfaced
    /// as <see cref="DepartmentStatus.Unknown"/> for downstream cards.
    /// Default 800ms per ADR 0080 §1 — sized so two sequential source
    /// calls still fit inside the <see cref="ProviderTimeout"/> budget.
    /// </summary>
    public TimeSpan PerSourceTimeout { get; set; } = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// Canonical defaults per ADR 0080 §1:
    /// <c>HeartbeatInterval = 30s</c>, <c>ProviderTimeout = 2s</c>,
    /// <c>PerSourceTimeout = 800ms</c>. Returns a fresh instance —
    /// callers MAY mutate the returned instance without affecting
    /// other callers.
    /// </summary>
    public static QuarterdeckOptions Default => new();
}

using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Host-configurable Tactical tunables per ADR 0081 §1 + §8.4. Bounds
/// are normative — Phase 2 implementations MUST throw
/// <see cref="InvalidOperationException"/> at DI registration when
/// any field violates its bound (heartbeat &gt; 0, alert TTL &gt; 0,
/// rate limits &gt; 0, capacities &gt; 0). The
/// <see cref="Default"/> singleton is the canonical baseline; hosts
/// override individual fields via <c>AddSunfishTactical(configure)</c>.
/// </summary>
public sealed class TacticalOptions
{
    /// <summary>Cadence at which <see cref="ITacticalDataProvider.SubscribeSnapshotAsync"/> emits heartbeats. Default 30s per ADR 0081 §1.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum simultaneously-active alerts per tenant. Eviction policy: LRU-by-DetectedAt. Default 200.</summary>
    public int MaxActiveAlerts { get; set; } = 200;

    /// <summary>Time-to-live for an alert in <see cref="AlertStatus.Active"/> without acknowledgement. After expiry, alert transitions to <see cref="AlertStatus.Expired"/> unless <see cref="TacticalAlert.RequiresAcknowledgement"/> is true. Default 24h.</summary>
    public TimeSpan AlertTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Maximum signals processed per <see cref="ITacticalRuleEngine.EvaluateStreamAsync"/> batch. Default 100.</summary>
    public int SignalBatchSize { get; set; } = 100;

    /// <summary>Maximum simultaneously-open incidents per tenant. Default 50.</summary>
    public int MaxActiveIncidents { get; set; } = 50;

    /// <summary>
    /// Maximum emergency Standing Orders <see cref="IThreatTriggerService.TryIssueAsync"/>
    /// may issue per minute <b>per tenant globally (across all rules
    /// and templates)</b> per ADR 0081 §4.1 + §8.5. Above the
    /// threshold, additional triggers emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssuanceFailed"/>
    /// with <c>denialReason="rate-limit"</c>. The partition is
    /// deliberately the (TenantId) tuple — partitioning by
    /// (TenantId, RuleName) would multiply the effective limit by the
    /// rule count, defeating the §4.1 anti-spoofing intent.
    /// Default 3.
    /// </summary>
    public int MaxEmergencyOrdersPerMinute { get; set; } = 3;

    /// <summary>Maximum alerts <see cref="IAlertRouter.RouteAsync"/> may route per (TenantId, RuleName) per minute per ADR 0081 §8.4. Above the threshold, additional alerts emit <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>. Default 60.</summary>
    public int MaxAlertsPerMinutePerRule { get; set; } = 60;

    /// <summary>
    /// Rule-name prefixes permitted to declare
    /// <see cref="AlertRoutingPolicy.HighPriorityLookout"/> routing per
    /// ADR 0081 §8.3. Default <c>["sunfish.*"]</c>. Rules with
    /// non-allowlisted prefixes are silently downgraded by Phase 2's
    /// <c>DefaultAlertRouter</c> to
    /// <see cref="AlertRoutingPolicy.InformationalSonar"/> with a
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
    /// audit event carrying
    /// <c>denialReason="high-priority-routing-not-allowlisted"</c>.
    /// </summary>
    public IReadOnlyList<string> AllowedHighPriorityRulePrefixes { get; set; } = new[] { "sunfish.*" };

    /// <summary>
    /// Canonical defaults per ADR 0081 §1: <c>HeartbeatInterval = 30s</c>,
    /// <c>MaxActiveAlerts = 200</c>, <c>AlertTtl = 24h</c>,
    /// <c>SignalBatchSize = 100</c>, <c>MaxActiveIncidents = 50</c>,
    /// <c>MaxEmergencyOrdersPerMinute = 3</c>,
    /// <c>MaxAlertsPerMinutePerRule = 60</c>. Returns a fresh
    /// instance — callers MAY mutate the returned instance without
    /// affecting other callers.
    /// </summary>
    public static TacticalOptions Default => new();
}

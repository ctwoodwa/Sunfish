using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Aggregate snapshot returned by
/// <see cref="ITacticalDataProvider.GetSnapshotAsync"/> per
/// ADR 0081 §1. Combines per-tenant active alerts (split into
/// Lookout-routed + all-active views), open incidents,
/// permission-pre-resolved capability flags, registered-rule count,
/// and signal-rate observability into one entry-point payload for
/// the Tactical UI.
/// </summary>
/// <param name="CapturedAt">
/// Wall-clock timestamp when the snapshot was assembled. Per cohort
/// precedent (W#46 / W#49 / W#50 / W#54 / W#55),
/// <see cref="DateTimeOffset"/> stands in for the hand-off's
/// <c>NodaTime.Instant</c> — NodaTime is not on Directory.Packages.props.
/// </param>
/// <param name="TenantId">Tenant the snapshot is scoped to.</param>
/// <param name="ActiveAlerts">All currently-active alerts (Sonar + Lookout combined).</param>
/// <param name="LookoutAlerts">Subset of <see cref="ActiveAlerts"/> with <see cref="AlertRoutingPolicy.HighPriorityLookout"/> — operator-visible.</param>
/// <param name="ActiveIncidents">Currently-open incidents.</param>
/// <param name="CanAccessFireControl">
/// Pre-resolved capability flag: actor has
/// <c>ShipAction.IssueEmergencyStandingOrder</c>. False for human
/// actors in v1 — only <c>ShipAction</c>-resolved system
/// principals can issue (ADR 0081 §4.1). The Tactical UI uses this
/// to render the Fire Control surface in a non-actionable state for
/// human operators.
/// </param>
/// <param name="CanAcknowledgeAlerts">Pre-resolved capability flag: actor has <c>ShipAction.AcknowledgeTacticalAlert</c>.</param>
/// <param name="RegisteredRuleCount">Number of rules currently registered with <see cref="ITacticalRuleEngine"/>.</param>
/// <param name="SignalRatePerMinute">Trailing-1m signal-evaluation rate (across all rules).</param>
/// <param name="IsPartialSnapshot">True when the snapshot was assembled past <see cref="TacticalOptions"/> timeouts; downstream consumers MUST treat absent fields as unknown.</param>
/// <param name="DegradedSubsystems">When <see cref="IsPartialSnapshot"/> is true, names of subsystems that timed out (e.g., <c>"sonar"</c>, <c>"incidents"</c>); null otherwise.</param>
public sealed record TacticalSnapshot(
    DateTimeOffset CapturedAt,
    TenantId TenantId,
    IReadOnlyList<TacticalAlert> ActiveAlerts,
    IReadOnlyList<TacticalAlert> LookoutAlerts,
    IReadOnlyList<IncidentRecord> ActiveIncidents,
    bool CanAccessFireControl,
    bool CanAcknowledgeAlerts,
    int RegisteredRuleCount,
    int SignalRatePerMinute,
    bool IsPartialSnapshot,
    IReadOnlyList<string>? DegradedSubsystems);

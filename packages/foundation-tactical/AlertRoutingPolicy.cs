using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Per-alert routing policy per ADR 0081 §2. Selects which destination
/// surface an alert is written to: <see cref="HighPriorityLookout"/>
/// surfaces the alert on the Lookout (operator-facing,
/// near-real-time); <see cref="InformationalSonar"/> writes to the
/// Sonar store (record-only, queryable, no operator notification).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertRoutingPolicy
{
    /// <summary>
    /// Operator-visible alert. <c>DefaultAlertRouter</c>
    /// (Phase 2) verifies the rule name matches the configured
    /// <c>AllowedHighPriorityRulePrefixes</c> before routing — non-
    /// allowlisted rules are downgraded to
    /// <see cref="InformationalSonar"/> and emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
    /// per ADR 0081 §8.3.
    /// </summary>
    HighPriorityLookout,

    /// <summary>
    /// Record-only alert; written to <see cref="ISonarStore"/> for
    /// later query/diagnostics. No operator notification is raised.
    /// </summary>
    InformationalSonar,
}

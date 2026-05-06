using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Routes a <see cref="TacticalAlert"/> to either
/// <see cref="ILookout"/> (operator-visible high-priority) or
/// <see cref="ISonarStore"/> (record-only) per the alert's
/// <see cref="TacticalAlert.RoutingPolicy"/>. Per ADR 0081 §2.
/// </summary>
/// <remarks>
/// <b>Order of operations (Phase 2 — <c>DefaultAlertRouter</c>):</b>
/// <list type="number">
/// <item><description>Validate <see cref="TacticalAlert.AlertId"/> regex
/// <c>^[A-Za-z0-9_\-\.:]{1,128}$</c>; on failure emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// with <c>denialReason="invalid-alert-id"</c> and return.</description></item>
/// <item><description>Enforce
/// <c>MaxAlertsPerMinutePerRule</c> per
/// <c>(TenantId, RuleName)</c>; on breach emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// with <c>denialReason="rule-rate-limit"</c> and return.</description></item>
/// <item><description>Emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.AnomalyDetected"/>.</description></item>
/// <item><description>Emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.AlertRouted"/>.</description></item>
/// <item><description>If
/// <see cref="AlertRoutingPolicy.HighPriorityLookout"/> AND the rule
/// name does NOT match the configured allowlist of high-priority
/// prefixes (§8.3), downgrade to
/// <see cref="AlertRoutingPolicy.InformationalSonar"/> and emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// with <c>denialReason="high-priority-routing-not-allowlisted"</c>.</description></item>
/// <item><description>Route to
/// <see cref="ILookout.WriteAsync"/> or
/// <see cref="ISonarStore.WriteAsync"/>. Audit events from steps 3-4
/// commit BEFORE this step; if the destination write fails, audit
/// records are retained and the failure is logged at Warning.</description></item>
/// </list>
/// <b>Latency budget:</b> Phase 2 implementations MUST complete
/// within 200ms; callers apply 250ms timeout as defense-in-depth.
/// </remarks>
public interface IAlertRouter
{
    /// <summary>Route the alert per the order-of-operations contract.</summary>
    ValueTask RouteAsync(TacticalAlert alert, CancellationToken ct = default);
}

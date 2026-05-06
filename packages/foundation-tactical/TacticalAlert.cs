using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// An alert emitted by an <see cref="ITacticalRule"/> per
/// ADR 0081 §1. Alerts route through <see cref="IAlertRouter"/> to
/// either <see cref="ILookout"/> (operator-visible) or
/// <see cref="ISonarStore"/> (record-only) per
/// <see cref="RoutingPolicy"/>.
/// </summary>
/// <param name="AlertId">
/// Stable identifier; durable across snapshot emits. Format:
/// <c>"{RuleName}:{source-local-id}"</c>; characters MUST match the
/// regex <c>^[A-Za-z0-9_\-\.:]{1,128}$</c> per ADR 0081 §1.
/// <see cref="IAlertRouter"/> validates the format and emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// with <c>denialReason="invalid-alert-id"</c> on violation.
/// </param>
/// <param name="TenantId">Tenant the alert belongs to.</param>
/// <param name="RuleName">Name of the rule that emitted this alert.</param>
/// <param name="Severity">Severity classification; drives Lookout sort + threat-trigger gating.</param>
/// <param name="RoutingPolicy">Per-alert routing policy.</param>
/// <param name="Title">Localized headline for the alert.</param>
/// <param name="Summary">One-sentence body. Empty string when the title alone conveys the alert.</param>
/// <param name="DetectedAt">
/// Wall-clock timestamp when the rule emitted the alert. Per cohort
/// precedent (W#46 / W#49 / W#50 / W#54 / W#55),
/// <see cref="DateTimeOffset"/> stands in for the hand-off's
/// <c>NodaTime.Instant</c> — NodaTime is not on Directory.Packages.props.
/// </param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="RequiresAcknowledgement">Whether an operator must acknowledge before the alert may transition to <see cref="AlertStatus.Expired"/>.</param>
/// <param name="RunbookStepIds">Optional ordered list of runbook-step identifiers for operator response.</param>
/// <param name="AcknowledgedBy">Actor who acknowledged the alert; null when <see cref="Status"/> ≠ <see cref="AlertStatus.Acknowledged"/>.</param>
/// <param name="AcknowledgedAt">Acknowledgement timestamp; null when <see cref="Status"/> ≠ <see cref="AlertStatus.Acknowledged"/>.</param>
/// <remarks>
/// <b>Acknowledgement-state invariant:</b> the tuple
/// <c>(Status, AcknowledgedBy, AcknowledgedAt)</c> follows two legal
/// shapes — <c>(Active|Expired|Superseded, null, null)</c> when never
/// acknowledged, or <c>(Acknowledged|Expired|Superseded, actorId,
/// timestamp)</c> when acknowledged. Phase 2 implementations MUST NOT
/// emit <c>(Acknowledged, null, _)</c> or <c>(Acknowledged, _, null)</c>.
/// </remarks>
public sealed record TacticalAlert(
    string AlertId,
    TenantId TenantId,
    string RuleName,
    AlertSeverity Severity,
    AlertRoutingPolicy RoutingPolicy,
    string Title,
    string Summary,
    DateTimeOffset DetectedAt,
    AlertStatus Status,
    bool RequiresAcknowledgement,
    IReadOnlyList<string> RunbookStepIds,
    ActorId? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAt);

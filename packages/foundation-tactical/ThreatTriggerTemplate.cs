using System;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// A registered template that
/// <see cref="IThreatTriggerService.TryIssueAsync"/> uses to mint an
/// emergency Standing Order when a matching <see cref="TacticalAlert"/>
/// crosses <see cref="MinimumSeverity"/>. Per ADR 0081 §4.
/// </summary>
/// <param name="RuleName">
/// Tactical rule whose alerts trigger this template. Matched
/// case-sensitively against
/// <see cref="TacticalAlert.RuleName"/>.
/// </param>
/// <param name="MinimumSeverity">
/// The lowest <see cref="AlertSeverity"/> that triggers issuance.
/// Alerts with a higher (more permissive) severity ordinal are
/// ignored — e.g., a template at <see cref="AlertSeverity.High"/>
/// fires on Critical + High but skips Medium / Low /
/// Informational.
/// </param>
/// <param name="OrderContent">Standing-Order body content the threat-trigger emits on issuance.</param>
/// <param name="ExpiresAfter">Optional auto-expiry; null = no expiry (the issued Standing Order persists until explicitly rescinded).</param>
public sealed record ThreatTriggerTemplate(
    string RuleName,
    AlertSeverity MinimumSeverity,
    string OrderContent,
    TimeSpan? ExpiresAfter);

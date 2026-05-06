using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Mints emergency Standing Orders from
/// <see cref="TacticalAlert"/> values that match registered
/// <see cref="ThreatTriggerTemplate"/> patterns. Per ADR 0081 §4.
/// Phase 1 ships the contract; Phase 2 wires the
/// <c>DefaultThreatTriggerService</c> implementation that resolves
/// the issuing principal via
/// <see cref="ISystemPrincipalProvider"/>, calls into the W#42
/// Standing-Order issuer, and emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssued"/>
/// on success or
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssuanceFailed"/>
/// on failure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Issuance contract:</b>
/// <see cref="TryIssueAsync"/> resolves the issuing principal
/// internally via <see cref="ISystemPrincipalProvider"/> per
/// §4.1 — callers do NOT supply a principal, ensuring emergency
/// orders cannot be socially-engineered through the threat-trigger
/// surface. Returns <c>null</c> when no template matches, the
/// severity threshold is not met, the rate limit
/// (<see cref="TacticalOptions.MaxEmergencyOrdersPerMinute"/>) is
/// breached, or the underlying issuance call denies/fails.
/// </para>
/// </remarks>
public interface IThreatTriggerService
{
    /// <summary>Register a template. Phase 2 enforces template-name uniqueness + the rule-name reservation rules.</summary>
    void RegisterTemplate(ThreatTriggerTemplate template);

    /// <summary>
    /// Attempt to mint an emergency Standing Order from
    /// <paramref name="alert"/> if any registered template matches
    /// the alert's <see cref="TacticalAlert.RuleName"/> + meets
    /// <see cref="ThreatTriggerTemplate.MinimumSeverity"/>. Returns
    /// the issued Standing-Order id on success; <c>null</c>
    /// otherwise.
    /// </summary>
    ValueTask<string?> TryIssueAsync(
        TacticalAlert alert,
        CancellationToken ct = default);
}

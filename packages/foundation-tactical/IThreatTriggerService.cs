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
/// <b>Issuance contract (8-step OOO per ADR 0081 §4):</b>
/// <see cref="TryIssueAsync"/> resolves the issuing principal
/// internally via <see cref="ISystemPrincipalProvider"/> per §4.1 —
/// callers do NOT supply a principal, ensuring emergency orders
/// cannot be socially-engineered through the threat-trigger surface.
/// Phase 2 implementations execute these steps in order:
/// </para>
/// <list type="number">
/// <item><description>Verify <c>alert.TenantId</c> matches the
/// ambient <c>ITenantContext.TenantId</c> per §8.2; on mismatch emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// with <c>denialReason="tenant-mismatch"</c> + return null.</description></item>
/// <item><description>Find a registered template matching
/// <c>alert.RuleName</c> case-sensitively; no match returns null.</description></item>
/// <item><description>Verify <c>alert.Severity</c> meets the
/// template's <see cref="ThreatTriggerTemplate.MinimumSeverity"/>
/// (lower-or-equal ordinal); fail returns null.</description></item>
/// <item><description>Check <see cref="TacticalOptions.MaxEmergencyOrdersPerMinute"/>
/// per (TenantId) globally; breach emits
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssuanceFailed"/>
/// with <c>denialReason="rate-limit"</c> + returns null.</description></item>
/// <item><description>Resolve the issuing principal via
/// <see cref="ISystemPrincipalProvider.GetSystemPrincipalAsync"/> +
/// verify the resolved principal's
/// <c>Sunfish.Foundation.Crypto.PrincipalId</c> equals the
/// implementation's bootstrap-pinned system PrincipalId
/// (defense-in-depth — see §4.1 + §8.1).</description></item>
/// <item><description>Issue the Standing Order via the W#42
/// <c>IStandingOrderRepository.AppendAsync</c> path.</description></item>
/// <item><description>On success emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssued"/>
/// + return the new Standing-Order id.</description></item>
/// <item><description>On any failure path, emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.EmergencyStandingOrderIssuanceFailed"/>
/// with a discriminating <c>denialReason</c> + return null.</description></item>
/// </list>
/// <para>
/// The audit-infrastructure-failure-only-skips-emit invariant from
/// <see cref="ITacticalCommandService"/> applies here too — silent
/// catch-and-continue MUST NOT be implemented around audit emits.
/// </para>
/// </remarks>
public interface IThreatTriggerService
{
    /// <summary>
    /// Register a template. Phase 2 enforces template-name uniqueness
    /// + the rule-name reservation rules + the
    /// <b>open-then-closed</b> contract: <see cref="RegisterTemplate"/>
    /// MUST be invoked at startup only — calls after the first signal
    /// is processed by <see cref="ITacticalRuleEngine"/> throw
    /// <see cref="System.InvalidOperationException"/>. The
    /// <c>ShipAction.ManageThreatTriggers</c> action remains reserved
    /// for v2 runtime template management and MUST NOT be granted to
    /// any role in v1 — there is no runtime-mutation path through
    /// this surface in v1.
    /// </summary>
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

using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>
/// Per-validator contract per ADR 0068 §2. Implementations MUST be
/// idempotent + side-effect free. Validators do not short-circuit
/// — every registered instance runs unconditionally; findings are
/// aggregated; issuance fails on any Error finding (§2.1).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public interface ISecurityPolicyValidator
{
    /// <summary>Ordering priority for <see cref="SecurityPolicyValidationResult.Findings"/>.</summary>
    SecurityPolicyValidatorPriority Priority { get; }

    /// <summary>
    /// Validate the <paramref name="proposed"/> policy against the
    /// <paramref name="current"/> projection. Returns findings;
    /// presence of Error severity blocks issuance at the caller.
    /// </summary>
    /// <remarks>
    /// TRUST CONTRACT: <paramref name="current"/> MUST be the
    /// issuer's authoritative projection from the immutable Standing-
    /// Order log (§3). Callers MUST NOT synthesize
    /// <paramref name="current"/> from untrusted input — a forged
    /// <c>current</c> could let a Consistency rule silently pass
    /// (e.g., omitting <c>CompromiseIndicatorFlagged</c> from
    /// <c>current.AutoTriggers</c> so the non-removability check
    /// trivially holds). The <see cref="ISecurityPolicyFloorValidator"/>
    /// is the absolute-invariant backstop and does not depend on
    /// <paramref name="current"/> for its core rules.
    /// </remarks>
    ValueTask<SecurityPolicyValidationResult> ValidateAsync(
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        SecurityPolicyValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for floor validators per ADR 0068 §2.1.1.
/// Floor validators are registered via
/// <c>AddSingleton&lt;ISecurityPolicyFloorValidator, T&gt;()</c>
/// — NOT via <c>TryAddSingleton</c> — so plugins cannot shadow the
/// platform floor. <c>FloorPolicyValidator</c> +
/// <c>RegulatoryValidator</c> (future PR) implement this.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public interface ISecurityPolicyFloorValidator : ISecurityPolicyValidator
{
}

namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>
/// Priority ordering for security-policy validators per ADR 0068 §2.
/// Governs the order findings appear in
/// <see cref="SecurityPolicyValidationResult.Findings"/> — NOT
/// short-circuit evaluation. All validators run unconditionally;
/// findings are aggregated; issuance fails when any
/// <see cref="SecurityPolicyValidationSeverity.Error"/> is present.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public enum SecurityPolicyValidatorPriority
{
    Schema       = 100,
    Consistency  = 200,
    FloorPolicy  = 300,
    Regulatory   = 400,
}

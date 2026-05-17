namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>
/// Aggregated result from running every registered
/// <see cref="ISecurityPolicyValidator"/> per ADR 0068 §2.
/// <see cref="IsValid"/> is derived from <see cref="Findings"/> — a
/// caller cannot construct an inconsistent
/// <c>(IsValid=true, Findings=[error])</c> pair. Warnings + Infos do
/// not block issuance.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record SecurityPolicyValidationResult(
    IReadOnlyList<SecurityPolicyValidationFinding> Findings)
{
    /// <summary>True when no <see cref="SecurityPolicyValidationSeverity.Error"/> findings are present.</summary>
    public bool IsValid => !Findings.Any(f => f.Severity == SecurityPolicyValidationSeverity.Error);

    public static SecurityPolicyValidationResult Empty { get; } =
        new(Array.Empty<SecurityPolicyValidationFinding>());
}

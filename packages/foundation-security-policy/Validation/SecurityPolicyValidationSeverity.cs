namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>Severity of a single <see cref="SecurityPolicyValidationFinding"/> per ADR 0068 §2.</summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public enum SecurityPolicyValidationSeverity
{
    Error,
    Warning,
    // Info severity removed per xo-council 2026-05-17 — no validator
    // in scope emits Info, and the unused-enum-value path would
    // bypass the WCAG factory guards on .Error()/.Warning(). Restore
    // when RegulatoryValidator (PR 3) needs informational findings
    // with a matching .Info() factory.
}

namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>
/// A single validation finding per ADR 0068 §2.
/// <see cref="Message"/> + <see cref="Suggestion"/> satisfy WCAG
/// 3.3.1 (plain-English description) + 3.3.3 (error suggestion).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record SecurityPolicyValidationFinding
{
    public SecurityPolicyValidationSeverity Severity { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Suggestion { get; init; } = string.Empty;

    /// <summary>Factory for an Error-severity finding — Code + Message + Suggestion all required (WCAG 3.3.1 + 3.3.3).</summary>
    public static SecurityPolicyValidationFinding Error(string code, string message, string suggestion)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code must be non-empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("WCAG 3.3.1 — Message must be non-empty plain-English.", nameof(message));
        if (string.IsNullOrWhiteSpace(suggestion))
            throw new ArgumentException("WCAG 3.3.3 — Suggestion must be non-empty.", nameof(suggestion));
        return new SecurityPolicyValidationFinding
        {
            Severity   = SecurityPolicyValidationSeverity.Error,
            Code       = code,
            Message    = message,
            Suggestion = suggestion,
        };
    }

    /// <summary>Factory for a Warning-severity finding — Code + Message + Suggestion all required (WCAG 3.3.3).</summary>
    public static SecurityPolicyValidationFinding Warning(string code, string message, string suggestion)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code must be non-empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must be non-empty.", nameof(message));
        if (string.IsNullOrWhiteSpace(suggestion))
            throw new ArgumentException("Suggestion must be non-empty.", nameof(suggestion));
        return new SecurityPolicyValidationFinding
        {
            Severity   = SecurityPolicyValidationSeverity.Warning,
            Code       = code,
            Message    = message,
            Suggestion = suggestion,
        };
    }
}

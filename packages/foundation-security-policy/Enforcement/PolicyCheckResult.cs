namespace Sunfish.Foundation.SecurityPolicy.Enforcement;

/// <summary>
/// Result of a single security-policy enforcement check per ADR 0068
/// §4. <see cref="AccessibleMessage"/> + <see cref="SuggestedAction"/>
/// are required-non-null on violations to satisfy WCAG 3.3.1 (plain-
/// English description) + 3.3.3 (error suggestion); the
/// <see cref="ViolationResult"/>
/// factory throws when either is missing.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record PolicyCheckResult
{
    public bool IsCompliant { get; init; }
    public PolicyViolationKind? Violation { get; init; }
    public string AccessibleMessage { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public System.TimeSpan? GracePeriodRemaining { get; init; }

    public static PolicyCheckResult Compliant() => new() { IsCompliant = true };

    public static PolicyCheckResult ViolationResult(
        PolicyViolationKind violation,
        string accessibleMessage,
        string suggestedAction,
        System.TimeSpan? gracePeriodRemaining = null)
    {
        if (string.IsNullOrWhiteSpace(accessibleMessage))
            throw new System.ArgumentException(
                "WCAG 3.3.1 — accessibleMessage must be non-empty plain-English.",
                nameof(accessibleMessage));
        if (string.IsNullOrWhiteSpace(suggestedAction))
            throw new System.ArgumentException(
                "WCAG 3.3.3 — suggestedAction must be non-empty.",
                nameof(suggestedAction));
        return new PolicyCheckResult
        {
            IsCompliant            = false,
            Violation              = violation,
            AccessibleMessage      = accessibleMessage,
            SuggestedAction        = suggestedAction,
            GracePeriodRemaining   = gracePeriodRemaining,
        };
    }
}

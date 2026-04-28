using Microsoft.CodeAnalysis;

namespace Sunfish.Analyzers.ProviderNeutrality;

/// <summary>
/// Diagnostic descriptor constants for the provider-neutrality analyzer.
/// </summary>
internal static class Diagnostics
{
    /// <summary>Category used on every diagnostic this analyzer reports.</summary>
    public const string Category = "Sunfish.ProviderNeutrality";

    /// <summary>
    /// SUNFISH_PROVNEUT_001 — vendor SDK namespace referenced from a non-providers
    /// package (a project under <c>packages/blocks-*/</c> or <c>packages/foundation-*/</c>).
    /// </summary>
    public const string ProviderNeutralityViolationId = "SUNFISH_PROVNEUT_001";

    /// <summary>
    /// SUNFISH_PROVNEUT_001 descriptor. Severity Error: ADR 0013 declares vendor-
    /// neutrality load-bearing — a leak here multiplies future swap costs by N callers.
    /// Message format: {0} = vendor namespace reference, {1} = current project's assembly name.
    /// </summary>
    public static readonly DiagnosticDescriptor ProviderNeutralityViolation = new(
        id: ProviderNeutralityViolationId,
        title: "Vendor SDK referenced from a non-providers package",
        messageFormat: "Vendor SDK namespace '{0}' is referenced from '{1}'. ADR 0013 restricts vendor SDK references to packages/providers-* projects.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ADR 0013 declares vendor-neutrality load-bearing: domain code in packages/blocks-* and packages/foundation-* must not reference vendor SDK namespaces (e.g. Stripe, Plaid, SendGrid, Twilio). Only packages/providers-* may take vendor-SDK dependencies. The contract seam Sunfish.Foundation.Integrations is excluded from this rule because it defines the vendor-neutral interfaces that providers implement.",
        helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0013-foundation-integrations.md");
}

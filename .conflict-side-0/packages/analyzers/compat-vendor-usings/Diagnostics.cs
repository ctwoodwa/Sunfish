using Microsoft.CodeAnalysis;

namespace Sunfish.Analyzers.CompatVendorUsings;

/// <summary>
/// Diagnostic descriptor constants for the compat-vendor-usings analyzer.
/// </summary>
internal static class Diagnostics
{
    /// <summary>Category used on every diagnostic this analyzer reports.</summary>
    public const string Category = "Sunfish.Compat";

    /// <summary>
    /// SF0001 — a vendor namespace was detected AND a Sunfish compat shim is available.
    /// Info severity; has a corresponding code fix that flips the using directive.
    /// </summary>
    public const string CompatShimAvailableId = "SF0001";

    /// <summary>
    /// SF0002 — a vendor namespace was detected BUT no Sunfish compat shim is available
    /// (e.g. DevExpress, dropped from compat scope per intake Decision 3 / 2026-04-22).
    /// Informational only; no code fix is registered.
    /// </summary>
    public const string CompatShimUnavailableId = "SF0002";

    /// <summary>
    /// SF0001 descriptor — vendor namespace has a compat replacement. Message format:
    /// {0} = vendor namespace, {1} = Sunfish.Compat.* replacement namespace.
    /// </summary>
    public static readonly DiagnosticDescriptor CompatShimAvailable = new(
        id: CompatShimAvailableId,
        title: "Vendor namespace has a Sunfish compat replacement",
        messageFormat: "Vendor namespace '{0}' detected; a Sunfish compat shim is available at '{1}'. Consider flipping the using to migrate.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Sunfish ships source-shape-compatible migration off-ramps for several vendor Blazor libraries. Replacing the vendor using with the Sunfish.Compat.* equivalent lets you migrate incrementally while keeping markup intact.",
        helpLinkUri: "https://github.com/your-org/sunfish/blob/main/packages/analyzers/compat-vendor-usings/README.md#sf0001");

    /// <summary>
    /// SF0002 descriptor — vendor namespace detected but no compat shim exists.
    /// Message format: {0} = vendor namespace, {1} = migration-guide path.
    /// </summary>
    public static readonly DiagnosticDescriptor CompatShimUnavailable = new(
        id: CompatShimUnavailableId,
        title: "Vendor namespace detected; no Sunfish compat shim is available",
        messageFormat: "Vendor namespace '{0}' detected; no Sunfish compat shim is available. See '{1}' for the manual migration path.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A vendor namespace was recognized by Sunfish's compat-vendor-usings analyzer, but Sunfish intentionally does not ship a compat shim for this vendor. Manual migration is required; follow the linked migration guide.",
        helpLinkUri: "https://github.com/your-org/sunfish/blob/main/packages/analyzers/compat-vendor-usings/README.md#sf0002");
}

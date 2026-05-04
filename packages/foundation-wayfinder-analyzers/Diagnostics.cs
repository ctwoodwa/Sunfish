using Microsoft.CodeAnalysis;

namespace Sunfish.Wayfinder.Analyzers;

/// <summary>
/// Diagnostic descriptors for the Wayfinder analyzer. Per ADR 0065 / W#42
/// Phase 3b.
/// </summary>
internal static class Diagnostics
{
    public const string SchemaRegistrationMissingId = "SUNFISH_WAYFINDER001";

    public static readonly DiagnosticDescriptor SchemaRegistrationMissing = new(
        id: SchemaRegistrationMissingId,
        title: "Wayfinder DI host is missing schema registration",
        messageFormat: "{0} registers Wayfinder via {1}() but does not declare any AtlasSchemaDescriptor; the Atlas form view will fall back to inferred kinds for every settable path",
        category: "SunfishWayfinder",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Per ADR 0065 §5 and the Phase 3b SchemaRegistrationAnalyzer rule: any project that calls IServiceCollection.AddSunfish*() to wire the Wayfinder substrate should also declare at least one AtlasSchemaDescriptor (typically via DefaultAtlasProjector.RegisterSchema) so the form view can render registered settings with the correct renderer + display name. Projects that ship without descriptors get the inferred-kind fallback, which is best-effort and lossy for Enum / Secret kinds.",
        helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0065-wayfinder-system-and-standing-order-contract.md",
        // Required by RS1037: the diagnostic is reported from a compilation-
        // end action (after the per-syntax-node walk completes). Cohort
        // precedent (loc-comments / loc-unused) uses the bare params form.
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}

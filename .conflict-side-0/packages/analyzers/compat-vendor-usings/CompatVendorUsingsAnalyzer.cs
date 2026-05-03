using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Analyzers.CompatVendorUsings;

/// <summary>
/// Detects vendor <c>using</c> directives (Telerik/Syncfusion/Infragistics and several
/// icon libraries) and emits either:
/// <list type="bullet">
///   <item><description>SF0001 — compat shim is available, code fix is offered.</description></item>
///   <item><description>SF0002 — vendor recognized but no shim; informational only.</description></item>
/// </list>
/// Unknown (non-vendor) namespaces are silent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompatVendorUsingsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            Diagnostics.CompatShimAvailable,
            Diagnostics.CompatShimUnavailable);

    public override void Initialize(AnalysisContext context)
    {
        // Standard best practice for analyzers: ignore generated code, enable concurrent
        // execution. Generated-code suppression is important for Blazor — the Razor
        // toolchain emits @(namespace)-qualified usings into generated .g.cs files that
        // should not be flagged.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;

        // Skip "using static", "using X = ..." aliases, and extern-alias. We only care
        // about plain namespace usings, because that is what the code-fix can sensibly
        // rewrite.
        if (usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) ||
            usingDirective.Alias is not null ||
            usingDirective.Name is null)
        {
            return;
        }

        var namespaceName = usingDirective.Name.ToString();

        if (!VendorNamespaceRegistry.TryResolve(namespaceName, out var entry))
        {
            return;
        }

        var location = usingDirective.GetLocation();

        switch (entry.Kind)
        {
            case VendorNamespaceRegistry.VendorEntryKind.CompatShimAvailable:
                // Replacement is guaranteed non-null by the registry for this kind.
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.CompatShimAvailable,
                    location,
                    properties: ImmutableDictionary.CreateRange(new[]
                    {
                        new System.Collections.Generic.KeyValuePair<string, string?>(
                            CodeFixProperties.VendorNamespaceKey, namespaceName),
                        new System.Collections.Generic.KeyValuePair<string, string?>(
                            CodeFixProperties.ReplacementNamespaceKey, entry.Replacement),
                    }),
                    namespaceName,
                    entry.Replacement));
                break;

            case VendorNamespaceRegistry.VendorEntryKind.NoShimAvailable:
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.CompatShimUnavailable,
                    location,
                    namespaceName,
                    entry.MigrationGuide ?? "docs/ (migration guide TBD)"));
                break;
        }
    }
}

/// <summary>
/// Property-bag keys used to pass context from the analyzer to the code-fix provider.
/// Roslyn's Diagnostic.Properties dictionary is the standard cross-boundary carrier.
/// </summary>
internal static class CodeFixProperties
{
    public const string VendorNamespaceKey = "VendorNamespace";
    public const string ReplacementNamespaceKey = "ReplacementNamespace";
}

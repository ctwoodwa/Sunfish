using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Analyzers.ProviderNeutrality;

/// <summary>
/// SUNFISH_PROVNEUT_001 — enforces ADR 0013 provider-neutrality at build time.
/// Domain code in <c>packages/blocks-*</c> and <c>packages/foundation-*</c> must
/// not reference vendor SDK namespaces; only <c>packages/providers-*</c> may.
/// </summary>
/// <remarks>
/// Two layers of gating:
///   1. <c>Directory.Build.props</c> auto-attaches the analyzer only to projects
///      whose <c>MSBuildProjectName</c> matches the predicate (blocks-*, foundation-*,
///      excluding integrations / tests / self). Outside those projects the analyzer
///      doesn't load at all — zero cost.
///   2. The analyzer additionally re-checks the compilation's assembly name in
///      each handler as defense in depth. If somehow attached to a providers-* or
///      integrations project (e.g. via a hand-written ProjectReference that bypasses
///      the predicate), the handler short-circuits and emits no diagnostics.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProviderNeutralityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Diagnostics.ProviderNeutralityViolation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
        context.RegisterSyntaxNodeAction(AnalyzeQualifiedName, SyntaxKind.QualifiedName);
    }

    /// <summary>
    /// Mirrors the <c>_SunfishProvNeutTarget</c> predicate in
    /// <c>Directory.Build.props</c>: include projects starting with
    /// <c>Sunfish.Blocks.</c> or <c>Sunfish.Foundation</c>, excluding the
    /// <c>Sunfish.Foundation.Integrations</c> contract seam, the analyzer itself,
    /// and any test project (project name ending in <c>.Tests</c>).
    /// </summary>
    internal static bool IsTargetCompilation(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return false;
        }

        var name = assemblyName!;

        if (name == "Sunfish.Foundation.Integrations" ||
            name == "Sunfish.Analyzers.ProviderNeutrality" ||
            name.EndsWith(".Tests", StringComparison.Ordinal))
        {
            return false;
        }

        return name.StartsWith("Sunfish.Blocks.", StringComparison.Ordinal) ||
               name.StartsWith("Sunfish.Foundation", StringComparison.Ordinal);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        if (!IsTargetCompilation(context.Compilation.AssemblyName))
        {
            return;
        }

        var usingDirective = (UsingDirectiveSyntax)context.Node;

        // Skip "using static" (no namespace import). We DO inspect alias usings
        // ("using X = Stripe.Y;") because the right-hand side imports the banned
        // namespace just as much as a plain using does.
        if (usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) ||
            usingDirective.Name is null)
        {
            return;
        }

        var namespaceName = usingDirective.Name.ToString();
        if (BannedVendorNamespaces.Match(namespaceName) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.ProviderNeutralityViolation,
            usingDirective.GetLocation(),
            namespaceName,
            context.Compilation.AssemblyName));
    }

    /// <summary>
    /// Catches fully-qualified inline references like
    /// <c>Stripe.PaymentIntents.Create()</c> that don't go through a using directive.
    /// We inspect QualifiedNameSyntax (type-context qualified names) and the
    /// leftmost identifier's namespace prefix.
    /// </summary>
    private static void AnalyzeQualifiedName(SyntaxNodeAnalysisContext context)
    {
        if (!IsTargetCompilation(context.Compilation.AssemblyName))
        {
            return;
        }

        var qualifiedName = (QualifiedNameSyntax)context.Node;

        // Only inspect outermost QualifiedName nodes — a parent QualifiedName already
        // contains us. This avoids duplicate diagnostics on Stripe.X.Y.Z chains.
        if (qualifiedName.Parent is QualifiedNameSyntax)
        {
            return;
        }

        // Skip QualifiedName inside a UsingDirective — the UsingDirective handler
        // already reports the violation; we don't want a duplicate at a sub-span.
        if (qualifiedName.Parent is UsingDirectiveSyntax ||
            qualifiedName.Parent is NameEqualsSyntax)
        {
            return;
        }

        var qualified = qualifiedName.ToString();
        if (BannedVendorNamespaces.Match(qualified) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.ProviderNeutralityViolation,
            qualifiedName.GetLocation(),
            qualified,
            context.Compilation.AssemblyName));
    }
}

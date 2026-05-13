using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Foundation.ShipsOffice.Analyzers;

/// <summary>
/// SUNFISH_SHIPSOFFICE_PERM001 — emits a Warning on calls to
/// <c>IShipsOfficeDataProvider.GetSnapshotAsync</c> or
/// <c>IShipsOfficeDataProvider.SearchAsync</c> that are not preceded by a
/// call to <c>IPermissionResolver.AuthorizeAsync</c> with
/// <c>ShipAction.ViewShipsOffice</c> in the same method body.
/// Per ADR 0083 §2 / W#55 Phase 2d.
/// </summary>
/// <remarks>
/// Uses semantic analysis to verify the invocation receiver is actually
/// <c>IShipsOfficeDataProvider</c> (or a concrete implementation) before
/// reporting — avoids false-positives on unrelated types that happen to
/// expose the same method names. Falls back to syntactic matching when the
/// semantic model cannot resolve the symbol (partial compilation).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShipsOfficePermissionAnalyzer : DiagnosticAnalyzer
{
    private const string DataProviderInterfaceName = "IShipsOfficeDataProvider";
    private const string GetSnapshotAsyncName = "GetSnapshotAsync";
    private const string SearchAsyncName = "SearchAsync";
    private const string AuthorizeAsyncName = "AuthorizeAsync";
    private const string ViewShipsOfficeName = "ViewShipsOffice";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Diagnostics.PermissionCheckMissing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethodBody, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMethodBody, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeMethodBody(SyntaxNodeAnalysisContext ctx)
    {
        var body = ctx.Node switch
        {
            MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
            ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
            _ => null,
        };

        if (body is null) return;

        var invocations = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        // Find data-provider calls: resolve symbol to confirm the receiver is
        // IShipsOfficeDataProvider (or an implementation). Fall back to syntactic
        // name match when the semantic model can't resolve the symbol.
        var dataProviderCalls = invocations.Where(inv =>
        {
            var name = GetSimpleName(inv.Expression);
            if (name is not (GetSnapshotAsyncName or SearchAsyncName)) return false;

            var symbol = ctx.SemanticModel.GetSymbolInfo(inv, ctx.CancellationToken).Symbol;
            if (symbol is null)
                return true; // partial compilation — be conservative, match by name

            // Accept if the method belongs to IShipsOfficeDataProvider itself
            // or to a type that implements it (containingType chain check).
            return IsDataProviderMember(symbol);
        }).ToList();

        if (dataProviderCalls.Count == 0) return;

        // Check whether the method body contains an AuthorizeAsync call that
        // references ViewShipsOffice in its argument list.
        var hasPermissionCheck = invocations.Any(inv =>
        {
            if (GetSimpleName(inv.Expression) != AuthorizeAsyncName) return false;
            return inv.ArgumentList.Arguments.Any(arg =>
                arg.ToString().Contains(ViewShipsOfficeName));
        });

        if (hasPermissionCheck) return;

        foreach (var call in dataProviderCalls)
        {
            var name = GetSimpleName(call.Expression) ?? "data-provider method";
            ctx.ReportDiagnostic(
                Diagnostic.Create(Diagnostics.PermissionCheckMissing, call.GetLocation(), name));
        }
    }

    private static bool IsDataProviderMember(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method) return false;
        var containingType = method.ContainingType;
        if (containingType is null) return false;
        if (containingType.Name == DataProviderInterfaceName) return true;
        // Check if any implemented interface matches.
        return containingType.AllInterfaces.Any(i => i.Name == DataProviderInterfaceName);
    }

    private static string? GetSimpleName(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        _ => null,
    };
}

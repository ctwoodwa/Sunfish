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
/// Detection is purely syntactic — we scan method bodies for the guarded
/// member-access names. This avoids needing symbol resolution (which requires
/// a full compilation) and keeps the analyzer fast in live-build scenarios.
/// Trade-off: false-positives are possible when non-Ships-Office methods share
/// the same member names, which is unlikely given the specific surface names.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShipsOfficePermissionAnalyzer : DiagnosticAnalyzer
{
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

        // Find all data-provider calls (GetSnapshotAsync / SearchAsync).
        var dataProviderCalls = invocations.Where(inv =>
        {
            var name = GetSimpleName(inv.Expression);
            return name is GetSnapshotAsyncName or SearchAsyncName;
        }).ToList();

        if (dataProviderCalls.Count == 0) return;

        // Check whether the method body contains an AuthorizeAsync call that
        // references ViewShipsOffice.
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

    private static string? GetSimpleName(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
        IdentifierNameSyntax id => id.Identifier.ValueText,
        _ => null,
    };
}

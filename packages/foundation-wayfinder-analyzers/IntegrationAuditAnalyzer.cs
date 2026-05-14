using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Wayfinder.Analyzers;

/// <summary>
/// SUNFISH_INTEGRATION_AUDIT001 — flags direct <c>new AuditPayload(...)</c>
/// construction in code that handles ADR 0067 integration events. Callers
/// must use the typed factory methods on <c>IntegrationAuditPayloads</c>
/// instead of free-form construction, which could admit forbidden fields
/// (credential values) per ADR 0067 §8.
/// </summary>
/// <remarks>
/// <para>
/// The rule fires on every <c>ObjectCreationExpressionSyntax</c> whose
/// type name is <c>AuditPayload</c> when found in a file whose namespace
/// or using-directives reference <c>Sunfish.Blocks.Integrations</c> or
/// <c>Sunfish.UICore.Wayfinder.Integrations</c>. Pure-syntax detection —
/// no symbol resolution required so the analyzer runs in "live" mode.
/// </para>
/// <para>
/// <b>Severity: Error</b> per ADR 0067 §H7. Free-form <c>AuditPayload</c>
/// construction in integration-config code is a security invariant
/// violation, not a style concern.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IntegrationAuditAnalyzer : DiagnosticAnalyzer
{
    private const string AuditPayloadTypeName = "AuditPayload";

    private static readonly DiagnosticDescriptor Rule = new(
        id: "SUNFISH_INTEGRATION_AUDIT001",
        title: "Direct AuditPayload construction in integration-config code is forbidden",
        messageFormat: "Use IntegrationAuditPayloads factory methods instead of constructing AuditPayload directly. "
                     + "Direct construction bypasses the ADR 0067 §8 credential-redaction allowlist.",
        category: "SunfishIntegrationSecurity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ADR 0067 §8: audit payloads for integration events must be constructed via "
                   + "IntegrationAuditPayloads factory methods. Free-form AuditPayload construction "
                   + "risks including credential values in audit records, which is a security violation.",
        helpLinkUri: "https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0067-atlas-integration-config-ui-surface.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeObjectCreation,
            SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var creation = (ObjectCreationExpressionSyntax)ctx.Node;

        // Check if the created type is AuditPayload
        var typeName = creation.Type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => null,
        };

        if (typeName != AuditPayloadTypeName) return;

        // Only flag in files with integration-related usings / namespace
        var root = creation.SyntaxTree.GetRoot(ctx.CancellationToken);
        if (!IsIntegrationContext(root)) return;

        // The factory class itself is allowed to construct AuditPayload
        if (IsInsideIntegrationAuditPayloadsClass(creation)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static bool IsIntegrationContext(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            string? text = node switch
            {
                UsingDirectiveSyntax u => u.Name?.ToString(),
                NamespaceDeclarationSyntax ns => ns.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
                _ => null,
            };
            if (text is null) continue;
            if (text.Contains("Sunfish.Blocks.Integrations") ||
                text.Contains("UICore.Wayfinder.Integrations"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInsideIntegrationAuditPayloadsClass(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent is not null)
        {
            if (parent is ClassDeclarationSyntax cls &&
                cls.Identifier.Text == "IntegrationAuditPayloads")
            {
                return true;
            }
            parent = parent.Parent;
        }
        return false;
    }
}

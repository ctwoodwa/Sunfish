using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sunfish.Analyzers.CompatVendorUsings;

/// <summary>
/// Code fix for SF0001: replaces a vendor <c>using</c> directive with the corresponding
/// Sunfish.Compat.* namespace. Preserves leading trivia (indentation, blank lines,
/// comments attached to the using) so the migrated file reads identically apart from
/// the qualified name.
///
/// Intentionally registered only for SF0001 — SF0002 (DevExpress) has no code fix
/// because no shim exists to flip to.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CompatVendorUsingsCodeFix))]
[Shared]
public sealed class CompatVendorUsingsCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(Diagnostics.CompatShimAvailableId);

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id != Diagnostics.CompatShimAvailableId)
            {
                continue;
            }

            var usingDirective = root
                .FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<UsingDirectiveSyntax>();

            if (usingDirective is null)
            {
                continue;
            }

            // Pull replacement namespace from the diagnostic properties. The analyzer
            // always sets this for SF0001; defense-in-depth against a future change.
            if (!diagnostic.Properties.TryGetValue(
                    CodeFixProperties.ReplacementNamespaceKey,
                    out var replacement) ||
                string.IsNullOrEmpty(replacement))
            {
                continue;
            }

            var title = $"Replace with 'using {replacement};'";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => ReplaceUsingAsync(
                        context.Document,
                        usingDirective,
                        replacement!,
                        ct),
                    equivalenceKey: $"SF0001::{replacement}"),
                diagnostic);
        }
    }

    private static async Task<Document> ReplaceUsingAsync(
        Document document,
        UsingDirectiveSyntax originalUsing,
        string replacementNamespace,
        CancellationToken cancellationToken)
    {
        var root = await document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        // Build the new Name node from a parsed qualified name. ParseName is the
        // idiomatic way to convert a "Foo.Bar.Baz" string into a NameSyntax — it
        // handles arbitrarily deep qualified names without manual tree construction.
        var newName = SyntaxFactory
            .ParseName(replacementNamespace)
            .WithTriviaFrom(originalUsing.Name!);

        var newUsing = originalUsing
            .WithName(newName)
            // Preserve leading trivia (indentation, blank lines, leading comments).
            .WithLeadingTrivia(originalUsing.GetLeadingTrivia())
            .WithTrailingTrivia(originalUsing.GetTrailingTrivia());

        // If the document already has "using {replacementNamespace};" elsewhere (e.g.
        // the user is collapsing Syncfusion.Blazor.Grids + Syncfusion.Blazor.Buttons,
        // both of which map to Sunfish.Compat.Syncfusion), remove the current node
        // rather than producing a duplicate using.
        var compilationUnit = root as CompilationUnitSyntax;
        var duplicateExists = compilationUnit is not null &&
            compilationUnit.Usings
                .Where(u => u != originalUsing)
                .Any(u => IsSameNamespace(u, replacementNamespace));

        SyntaxNode newRoot;
        if (duplicateExists)
        {
            newRoot = root.RemoveNode(originalUsing, SyntaxRemoveOptions.KeepLeadingTrivia)
                ?? root;
        }
        else
        {
            newRoot = root.ReplaceNode(originalUsing, newUsing);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsSameNamespace(UsingDirectiveSyntax u, string candidate)
    {
        if (u.Alias is not null || u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
        {
            return false;
        }

        return u.Name is not null && u.Name.ToString() == candidate;
    }
}

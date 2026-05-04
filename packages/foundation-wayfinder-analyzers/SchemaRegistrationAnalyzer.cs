using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Wayfinder.Analyzers;

/// <summary>
/// SUNFISH_WAYFINDER001 — emits a Warning on every <c>AddSunfish*()</c>
/// invocation in a project that doesn't also declare an
/// <c>AtlasSchemaDescriptor</c>. Per ADR 0065 §5 / W#42 Phase 3b.
/// </summary>
/// <remarks>
/// <para>
/// The rule fires once per <c>AddSunfish*</c> call site, not once per
/// project. If the project has multiple <c>AddSunfish*</c> calls and no
/// schema-descriptor registrations, every call site gets a diagnostic.
/// Adding one descriptor anywhere in the project clears the warning at
/// every call site.
/// </para>
/// <para>
/// Detection is purely syntactic — we look for
/// <c>InvocationExpressionSyntax</c> whose name pattern starts with
/// <c>AddSunfish</c>, and for <c>ObjectCreationExpressionSyntax</c>
/// whose constructed-type name is <c>AtlasSchemaDescriptor</c>. We do
/// not consult symbol info because the analyzer must run cleanly against
/// projects that haven't compiled yet (Roslyn's "live build" pipeline).
/// The cost is some false positives — e.g., a method named
/// <c>AddSunfishCustomThing</c> that's unrelated to Wayfinder still
/// triggers the warning. Council F4 in the W#42 P1 review accepted this
/// trade-off.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SchemaRegistrationAnalyzer : DiagnosticAnalyzer
{
    private const string AddSunfishPrefix = "AddSunfish";
    private const string SchemaDescriptorTypeName = "AtlasSchemaDescriptor";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Diagnostics.SchemaRegistrationMissing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var perCompilation = new PerCompilationState();

            compilationStart.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, perCompilation),
                SyntaxKind.InvocationExpression);

            compilationStart.RegisterSyntaxNodeAction(
                ctx => AnalyzeObjectCreation(ctx, perCompilation),
                SyntaxKind.ObjectCreationExpression);

            compilationStart.RegisterCompilationEndAction(compilationEnd =>
            {
                if (perCompilation.HasSchemaDescriptor)
                {
                    return;
                }
                foreach (var entry in perCompilation.AddSunfishCallSites)
                {
                    var diagnostic = Diagnostic.Create(
                        Diagnostics.SchemaRegistrationMissing,
                        entry.Location,
                        compilationEnd.Compilation.AssemblyName ?? "(unknown assembly)",
                        entry.MethodName);
                    compilationEnd.ReportDiagnostic(diagnostic);
                }
            });
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, PerCompilationState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetInvokedSimpleName(invocation, out var methodName) &&
            methodName.StartsWith(AddSunfishPrefix, StringComparison.Ordinal))
        {
            state.AddSunfishCallSites.Add(
                new AddSunfishCallSite(invocation.GetLocation(), methodName));
        }
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, PerCompilationState state)
    {
        if (state.HasSchemaDescriptor)
        {
            return;
        }
        // Target-typed `new()` is intentionally NOT handled — granting it a
        // descriptor would be a false-NEGATIVE (e.g., a project doing
        // `services.AddSunfishWayfinder(); var x = new(); /* unrelated */`
        // would silently suppress the warning everywhere). Cohort precedent
        // (ProviderNeutralityAnalyzer) likewise ignores target-typed `new()`.
        if (context.Node is ObjectCreationExpressionSyntax explicitCreation
            && GetSimpleTypeName(explicitCreation.Type) is { } explicitName
            && explicitName == SchemaDescriptorTypeName)
        {
            state.HasSchemaDescriptor = true;
        }
    }

    private static bool TryGetInvokedSimpleName(InvocationExpressionSyntax invocation, out string methodName)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
                methodName = memberAccess.Name.Identifier.ValueText;
                return true;
            case IdentifierNameSyntax identifier:
                methodName = identifier.Identifier.ValueText;
                return true;
            case GenericNameSyntax generic:
                methodName = generic.Identifier.ValueText;
                return true;
            default:
                methodName = string.Empty;
                return false;
        }
    }

    private static string? GetSimpleTypeName(TypeSyntax typeSyntax)
    {
        return typeSyntax switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null,
        };
    }

    /// <summary>
    /// Per-compilation mutable state. The analyzer is concurrent (per
    /// <see cref="AnalysisContext.EnableConcurrentExecution"/>); both
    /// fields use thread-safe collections / atomic writes accordingly.
    /// </summary>
    private sealed class PerCompilationState
    {
        public ConcurrentBag<AddSunfishCallSite> AddSunfishCallSites { get; } = new();
        // bool writes are atomic on every Roslyn-supported runtime; the only
        // contention is the read in AnalyzeObjectCreation which short-circuits
        // when already true. False sets are safe.
        public bool HasSchemaDescriptor { get; set; }
    }

    private readonly struct AddSunfishCallSite
    {
        public AddSunfishCallSite(Location location, string methodName)
        {
            Location = location;
            MethodName = methodName;
        }

        public Location Location { get; }
        public string MethodName { get; }
    }
}

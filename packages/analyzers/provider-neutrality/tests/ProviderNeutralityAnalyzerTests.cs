using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Analyzers.ProviderNeutrality.Tests;

/// <summary>
/// Tests for <see cref="ProviderNeutralityAnalyzer"/>. The analyzer's behavior is
/// gated by the compilation's assembly name (mirrors the auto-attach predicate in
/// Directory.Build.props). Tests use SolutionTransforms.WithProjectAssemblyName to
/// simulate different package contexts.
/// </summary>
public class ProviderNeutralityAnalyzerTests
{
    private sealed class Verify : CSharpAnalyzerTest<ProviderNeutralityAnalyzer, DefaultVerifier>
    {
    }

    private static Task RunAsync(string assemblyName, string source, params DiagnosticResult[] expected)
    {
        var test = new Verify
        {
            TestCode = source,
            // The vendor namespaces in test sources don't exist in the test compilation's
            // reference set, which would otherwise produce CS0246 errors that are unrelated
            // to what this analyzer tests. Suppress compiler-diagnostic verification.
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.SolutionTransforms.Add((solution, projectId) =>
            solution.WithProjectAssemblyName(projectId, assemblyName));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // === Positive: rule fires in blocks-* and foundation-* ===

    [Fact]
    public Task Stripe_using_in_blocks_emits_diagnostic()
    {
        const string source = @"
{|#0:using Stripe;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("Stripe", "Sunfish.Blocks.Accounting");
        return RunAsync("Sunfish.Blocks.Accounting", source, expected);
    }

    [Fact]
    public Task Plaid_child_namespace_in_foundation_emits_diagnostic()
    {
        const string source = @"
{|#0:using Plaid.Banking;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("Plaid.Banking", "Sunfish.Foundation.MultiTenancy");
        return RunAsync("Sunfish.Foundation.MultiTenancy", source, expected);
    }

    [Fact]
    public Task SendGrid_using_in_blocks_emits_diagnostic()
    {
        const string source = @"
{|#0:using SendGrid.Helpers.Mail;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("SendGrid.Helpers.Mail", "Sunfish.Blocks.Accounting");
        return RunAsync("Sunfish.Blocks.Accounting", source, expected);
    }

    [Fact]
    public Task Twilio_using_in_blocks_emits_diagnostic()
    {
        const string source = @"
{|#0:using Twilio.Rest.Api.V2010.Account;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("Twilio.Rest.Api.V2010.Account", "Sunfish.Blocks.RentCollection");
        return RunAsync("Sunfish.Blocks.RentCollection", source, expected);
    }

    [Fact]
    public Task Inline_qualified_reference_emits_diagnostic()
    {
        // No using directive — fully-qualified inline reference. The analyzer's
        // QualifiedName walker catches this so `Stripe.X` evasions still fail.
        const string source = @"
namespace N
{
    class C
    {
        {|#0:Stripe.PaymentIntent|}? P;
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("Stripe.PaymentIntent", "Sunfish.Blocks.Accounting");
        return RunAsync("Sunfish.Blocks.Accounting", source, expected);
    }

    [Fact]
    public Task Aliased_using_emits_diagnostic()
    {
        // "using S = Stripe.X;" still imports Stripe.X — should still fail.
        const string source = @"
{|#0:using S = Stripe.PaymentIntent;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.ProviderNeutralityViolation)
            .WithLocation(0)
            .WithArguments("Stripe.PaymentIntent", "Sunfish.Blocks.Accounting");
        return RunAsync("Sunfish.Blocks.Accounting", source, expected);
    }

    // === Negative: rule does NOT fire in providers-* ===

    [Fact]
    public Task Stripe_using_in_providers_is_silent()
    {
        const string source = @"
using Stripe;

namespace N { class C { } }
";
        return RunAsync("Sunfish.Providers.Stripe", source);
    }

    [Fact]
    public Task Inline_qualified_reference_in_providers_is_silent()
    {
        const string source = @"
namespace N
{
    class C
    {
        Stripe.PaymentIntent? P;
    }
}
";
        return RunAsync("Sunfish.Providers.Stripe", source);
    }

    // === Negative: rule does NOT fire in Foundation.Integrations (the contract seam) ===

    [Fact]
    public Task Stripe_using_in_foundation_integrations_is_silent()
    {
        const string source = @"
using Stripe;

namespace N { class C { } }
";
        return RunAsync("Sunfish.Foundation.Integrations", source);
    }

    // === Negative: rule does NOT fire in test projects ===

    [Fact]
    public Task Stripe_using_in_test_project_is_silent()
    {
        const string source = @"
using Stripe;

namespace N { class C { } }
";
        return RunAsync("Sunfish.Blocks.Accounting.Tests", source);
    }

    // === Negative: non-vendor namespaces are silent ===

    [Fact]
    public Task System_using_is_silent()
    {
        const string source = @"
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace N { class C { } }
";
        return RunAsync("Sunfish.Blocks.Accounting", source);
    }

    [Fact]
    public Task Prefix_match_does_not_cross_identifier_boundary()
    {
        // "StripeFake" starts with "Stripe" lexically but NOT at a '.' boundary —
        // must not match.
        const string source = @"
using StripeFake;
using PlaidFake.Banking;

namespace N { class C { } }
";
        return RunAsync("Sunfish.Blocks.Accounting", source);
    }
}

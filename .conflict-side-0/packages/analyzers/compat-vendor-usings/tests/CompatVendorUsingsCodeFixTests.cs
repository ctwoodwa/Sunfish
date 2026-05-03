using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Analyzers.CompatVendorUsings.Tests;

/// <summary>
/// Tests for <see cref="CompatVendorUsingsCodeFix"/>. Each test provides an input
/// document and the expected rewritten document after applying the code fix.
/// </summary>
public class CompatVendorUsingsCodeFixTests
{
    private sealed class Verify : CSharpCodeFixTest<
        CompatVendorUsingsAnalyzer,
        CompatVendorUsingsCodeFix,
        DefaultVerifier>
    {
    }

    private static Task RunFixAsync(
        string source,
        string fixedSource,
        params DiagnosticResult[] expected)
    {
        var test = new Verify
        {
            TestCode = source,
            FixedCode = fixedSource,
            // The vendor namespaces exercised here don't exist in the test
            // compilation's reference set — CS0246 from the compiler is
            // expected and irrelevant to what the code fix transforms.
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    public Task Telerik_using_is_replaced_with_Sunfish_Compat_Telerik()
    {
        const string source = @"
{|#0:using Telerik.Blazor.Components;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.Telerik;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Telerik.Blazor.Components", "Sunfish.Compat.Telerik");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task Syncfusion_root_using_is_replaced_with_Sunfish_Compat_Syncfusion()
    {
        const string source = @"
{|#0:using Syncfusion.Blazor;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.Syncfusion;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Syncfusion.Blazor", "Sunfish.Compat.Syncfusion");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task Syncfusion_child_using_collapses_to_Sunfish_Compat_Syncfusion()
    {
        const string source = @"
{|#0:using Syncfusion.Blazor.Grids;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.Syncfusion;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Syncfusion.Blazor.Grids", "Sunfish.Compat.Syncfusion");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task Infragistics_child_using_collapses_to_Sunfish_Compat_Infragistics()
    {
        const string source = @"
{|#0:using IgniteUI.Blazor.Controls;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.Infragistics;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("IgniteUI.Blazor.Controls", "Sunfish.Compat.Infragistics");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task FontAwesome_Sharp_is_replaced_with_Sunfish_Compat_FontAwesome()
    {
        const string source = @"
{|#0:using FontAwesome.Sharp;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.FontAwesome;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("FontAwesome.Sharp", "Sunfish.Compat.FontAwesome");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task FluentUI_Icons_is_replaced_with_Sunfish_Compat_FluentIcons()
    {
        const string source = @"
{|#0:using Microsoft.FluentUI.AspNetCore.Components.Icons;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.FluentIcons;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments(
                "Microsoft.FluentUI.AspNetCore.Components.Icons",
                "Sunfish.Compat.FluentIcons");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task Code_fix_preserves_leading_trivia_and_surrounding_usings()
    {
        // The analyzer should only change the Telerik line; the preceding using and
        // trailing blank line are preserved.
        const string source = @"
using System;

{|#0:using Telerik.Blazor.Components;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using System;

using Sunfish.Compat.Telerik;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Telerik.Blazor.Components", "Sunfish.Compat.Telerik");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public Task Code_fix_removes_duplicate_when_replacement_already_present()
    {
        // User has both Syncfusion.Blazor and Sunfish.Compat.Syncfusion — fixing the
        // Syncfusion line should remove it (the target using is already present)
        // rather than duplicate.
        const string source = @"
using Sunfish.Compat.Syncfusion;
{|#0:using Syncfusion.Blazor.Grids;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.Syncfusion;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Syncfusion.Blazor.Grids", "Sunfish.Compat.Syncfusion");
        return RunFixAsync(source, fixedSource, expected);
    }

    [Fact]
    public async Task DevExpress_has_no_code_fix_registered()
    {
        // SF0002 has no fix. We verify this by running the analyzer test (which
        // only checks diagnostics, not code fixes) and asserting that the
        // diagnostic fires — the CodeFixProvider's FixableDiagnosticIds does not
        // include SF0002, so Roslyn would never invoke the fix for it.
        var analyzerTest = new CSharpAnalyzerTest<CompatVendorUsingsAnalyzer, DefaultVerifier>
        {
            TestCode = @"
{|#0:using DevExpress.Blazor;|}

namespace N { class C { } }
",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        analyzerTest.ExpectedDiagnostics.Add(
            new DiagnosticResult(Diagnostics.CompatShimUnavailable)
                .WithLocation(0)
                .WithArguments("DevExpress.Blazor", "docs/devexpress-migration.md"));
        await analyzerTest.RunAsync();

        // Assert directly on the CodeFixProvider's fixable-IDs list: SF0002 must
        // NOT appear. This is the load-bearing invariant — if someone accidentally
        // added SF0002 to the list, this assertion would catch it.
        var fixer = new CompatVendorUsingsCodeFix();
        Assert.DoesNotContain(
            Diagnostics.CompatShimUnavailableId,
            fixer.FixableDiagnosticIds);
        Assert.Contains(
            Diagnostics.CompatShimAvailableId,
            fixer.FixableDiagnosticIds);
    }

    [Fact]
    public Task BlazorBootstrap_Icons_is_replaced_with_Sunfish_Compat_BootstrapIcons()
    {
        const string source = @"
{|#0:using BlazorBootstrap.Icons;|}

namespace N { class C { } }
";
        const string fixedSource = @"
using Sunfish.Compat.BootstrapIcons;

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("BlazorBootstrap.Icons", "Sunfish.Compat.BootstrapIcons");
        return RunFixAsync(source, fixedSource, expected);
    }
}

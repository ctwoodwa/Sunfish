using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Analyzers.CompatVendorUsings.Tests;

/// <summary>
/// Tests for <see cref="CompatVendorUsingsAnalyzer"/>. Each test verifies that the
/// analyzer emits the expected diagnostic (ID + span) for a given input, using the
/// XUnit analyzer-testing harness.
/// </summary>
public class CompatVendorUsingsAnalyzerTests
{
    private sealed class Verify : CSharpAnalyzerTest<CompatVendorUsingsAnalyzer, DefaultVerifier>
    {
    }

    private static Task RunAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Verify
        {
            TestCode = source,
            // The vendor namespaces we exercise don't actually exist in the test
            // compilation's reference set, which would otherwise produce CS0246
            // "namespace not found" errors that are unrelated to what this
            // analyzer tests. Suppress compiler-diagnostic verification — we
            // care only about our SF0001/SF0002 diagnostics.
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // === SF0001: commercial vendors ===

    [Fact]
    public Task Telerik_using_emits_SF0001()
    {
        // Expected diagnostic span covers the full using directive
        // (including the "using " keyword and terminating semicolon).
        const string source = @"
{|#0:using Telerik.Blazor.Components;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Telerik.Blazor.Components", "Sunfish.Compat.Telerik");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task Syncfusion_child_namespace_emits_SF0001()
    {
        const string source = @"
{|#0:using Syncfusion.Blazor.Grids;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Syncfusion.Blazor.Grids", "Sunfish.Compat.Syncfusion");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task Syncfusion_root_namespace_emits_SF0001()
    {
        const string source = @"
{|#0:using Syncfusion.Blazor;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Syncfusion.Blazor", "Sunfish.Compat.Syncfusion");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task Infragistics_controls_namespace_emits_SF0001()
    {
        const string source = @"
{|#0:using IgniteUI.Blazor.Controls;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("IgniteUI.Blazor.Controls", "Sunfish.Compat.Infragistics");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task Infragistics_root_namespace_emits_SF0001()
    {
        const string source = @"
{|#0:using IgniteUI.Blazor;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("IgniteUI.Blazor", "Sunfish.Compat.Infragistics");
        return RunAsync(source, expected);
    }

    // === SF0001: icon libraries ===

    [Fact]
    public Task Blazored_FontAwesome_emits_SF0001()
    {
        const string source = @"
{|#0:using Blazored.FontAwesome;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Blazored.FontAwesome", "Sunfish.Compat.FontAwesome");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task FontAwesome_Sharp_emits_SF0001()
    {
        const string source = @"
{|#0:using FontAwesome.Sharp;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("FontAwesome.Sharp", "Sunfish.Compat.FontAwesome");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task FluentUI_Icons_emits_SF0001()
    {
        const string source = @"
{|#0:using Microsoft.FluentUI.AspNetCore.Components.Icons;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments(
                "Microsoft.FluentUI.AspNetCore.Components.Icons",
                "Sunfish.Compat.FluentIcons");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task BlazorBootstrap_Icons_emits_SF0001_but_root_BlazorBootstrap_is_silent()
    {
        // Icons sub-namespace: flagged.
        const string iconsSource = @"
{|#0:using BlazorBootstrap.Icons;|}

namespace N { class C { } }
";
        var iconsExpected = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("BlazorBootstrap.Icons", "Sunfish.Compat.BootstrapIcons");

        return RunAsync(iconsSource, iconsExpected);
    }

    [Fact]
    public Task BlazorBootstrap_root_is_silent()
    {
        // The root BlazorBootstrap namespace should NOT fire — only the Icons sub-ns
        // is registered.
        const string source = @"
using BlazorBootstrap;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    // === SF0002: DevExpress ===

    [Fact]
    public Task DevExpress_emits_SF0002_not_SF0001()
    {
        const string source = @"
{|#0:using DevExpress.Blazor;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimUnavailable)
            .WithLocation(0)
            .WithArguments("DevExpress.Blazor", "docs/devexpress-migration.md");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task DevExpress_child_namespace_also_emits_SF0002()
    {
        const string source = @"
{|#0:using DevExpress.Blazor.Grid;|}

namespace N { class C { } }
";
        var expected = new DiagnosticResult(Diagnostics.CompatShimUnavailable)
            .WithLocation(0)
            .WithArguments("DevExpress.Blazor.Grid", "docs/devexpress-migration.md");
        return RunAsync(source, expected);
    }

    // === Negative cases ===

    [Fact]
    public Task System_using_is_silent()
    {
        const string source = @"
using System;
using System.Collections.Generic;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    [Fact]
    public Task AspNetCore_Components_is_silent()
    {
        const string source = @"
using Microsoft.AspNetCore.Components;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    [Fact]
    public Task Sunfish_UIAdapters_Blazor_is_silent()
    {
        const string source = @"
using Sunfish.UIAdapters.Blazor;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    [Fact]
    public Task Prefix_match_does_not_cross_identifier_boundary()
    {
        // "SyncfusionFake.Blazor" should NOT match the "Syncfusion.Blazor" prefix —
        // the match must terminate at a '.' boundary.
        const string source = @"
using SyncfusionFake.Blazor;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    [Fact]
    public Task Using_alias_is_silent()
    {
        // "using X = Telerik.Blazor.Components;" is an alias directive, not a
        // namespace using. Skipped intentionally (code fix cannot meaningfully
        // rewrite an alias).
        const string source = @"
using TBC = Telerik.Blazor.Components;

namespace N { class C { } }
";
        return RunAsync(source);
    }

    [Fact]
    public Task Multiple_vendor_usings_each_emit_once()
    {
        // Diagnostic cacheability: two vendor usings in the same file each emit
        // exactly one diagnostic. (The test harness would flag duplicates.)
        const string source = @"
{|#0:using Telerik.Blazor.Components;|}
{|#1:using Syncfusion.Blazor.Grids;|}

namespace N { class C { } }
";
        var telerik = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(0)
            .WithArguments("Telerik.Blazor.Components", "Sunfish.Compat.Telerik");
        var syncfusion = new DiagnosticResult(Diagnostics.CompatShimAvailable)
            .WithLocation(1)
            .WithArguments("Syncfusion.Blazor.Grids", "Sunfish.Compat.Syncfusion");
        return RunAsync(source, telerik, syncfusion);
    }
}

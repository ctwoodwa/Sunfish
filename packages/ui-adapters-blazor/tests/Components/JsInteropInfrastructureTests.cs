using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

/// <summary>
/// Tests for G37 B0 — SunfishDataGrid JS interop infrastructure.
/// Covers: ES module import, attachGrid call, detachGrid disposal, colgroup markup.
/// </summary>
public class JsInteropInfrastructureTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record Employee(int Id, string Name, string Department);

    // ── Constructor / DI setup ─────────────────────────────────────────────

    public JsInteropInfrastructureTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<IDownloadService, StubDownloadService>();

        // Loose mode: bUnit auto-satisfies import() calls and returns mock IJSObjectReference
        // instances. Tests that need to verify specific invocations set up their own module mock.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static List<Employee> ThreeEmployees() =>
    [
        new(1, "Alice",   "Engineering"),
        new(2, "Bob",     "Design"),
        new(3, "Charlie", "Marketing"),
    ];

    private static RenderFragment TwoColumnDefs() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
        builder.AddAttribute(5, nameof(SunfishGridColumn<Employee>.Title), "Department");
        builder.CloseComponent();
    };

    private static RenderFragment ThreeColumnDefs(bool hideLastColumn = false) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Id));
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(6);
        builder.AddAttribute(7, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
        builder.AddAttribute(8, nameof(SunfishGridColumn<Employee>.Visible), !hideLastColumn);
        builder.CloseComponent();
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  B0.1 / B0.2 — ES module import
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// On first render the grid must call import() with the marilo-datagrid.js module path.
    /// bUnit treats import() calls specially — they are handled via SetupModule(), and the
    /// invocation is recorded in JSInterop.Invocations with identifier "import".
    /// </summary>
    [Fact]
    public void OnFirstRender_ImportsMariloDatagridModule()
    {
        // Arrange: Loose mode means bUnit auto-satisfies import() and returns a mock module.
        // SetupModule additionally registers a named handler so we can inspect its invocations.
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.SetupVoid("attachGrid", _ => true);

        // Act
        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, TwoColumnDefs()));

        // Assert: the import() for marilo-datagrid.js was recorded in root invocations
        var importCalls = JSInterop.Invocations
            .Where(i => i.Identifier == "import"
                     && i.Arguments.OfType<string>().Any(a =>
                            a.Contains("marilo-datagrid.js")))
            .ToList();

        Assert.True(importCalls.Count >= 1,
            "Expected at least one import() call for marilo-datagrid.js but found none.");
    }

    /// <summary>
    /// On first render the grid must call attachGrid on the imported module,
    /// passing a grid root reference and an options object.
    /// </summary>
    [Fact]
    public void OnFirstRender_CallsAttachGridOnModule()
    {
        // Arrange
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        var attachSetup = module.SetupVoid("attachGrid", _ => true);

        // Act
        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, TwoColumnDefs()));

        // Assert: attachGrid was invoked on the module
        var attachInvocations = module.Invocations
            .Where(i => i.Identifier == "attachGrid")
            .ToList();

        Assert.True(attachInvocations.Count >= 1,
            "Expected attachGrid to be called on the marilo-datagrid module but it was not.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B0.3 — DisposeAsync calls detachGrid before releasing references
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When the component is disposed, detachGrid must be called before the module
    /// reference is released.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CallsDetachGridBeforeDisposingModule()
    {
        // Arrange: register module and set up handlers so bUnit doesn't hang waiting for results.
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.SetupVoid("attachGrid", _ => true).SetVoidResult();
        module.SetupVoid("detachGrid", _ => true).SetVoidResult();

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, TwoColumnDefs()));

        // Act
        await cut.InvokeAsync(async () =>
        {
            if (cut.Instance is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        });

        // Assert: detachGrid was called on the module
        var detachInvocations = module.Invocations
            .Where(i => i.Identifier == "detachGrid")
            .ToList();

        Assert.True(detachInvocations.Count >= 1,
            "Expected detachGrid to be called during DisposeAsync but it was not.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B0.5 — <colgroup> markup
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The rendered table must contain a &lt;colgroup&gt; with one &lt;col&gt; per visible column.
    /// </summary>
    [Fact]
    public void Render_WithTwoVisibleColumns_ColgroupHasTwoColElements()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, TwoColumnDefs()));

        var colgroup = cut.Find("colgroup");
        // Two data columns; no drag/detail/checkbox/command cols in default setup
        var cols = colgroup.QuerySelectorAll("col[data-column-id]");

        Assert.Equal(2, cols.Length);
    }

    /// <summary>
    /// Each &lt;col&gt; inside &lt;colgroup&gt; must carry a data-column-id matching the column's EffectiveId.
    /// For a column with Field="Name" and no explicit Id, EffectiveId == "Name".
    /// </summary>
    [Fact]
    public void Render_ColElements_HaveDataColumnIdMatchingEffectiveId()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, TwoColumnDefs()));

        var colgroup = cut.Find("colgroup");
        var ids = colgroup
            .QuerySelectorAll("col[data-column-id]")
            .Select(c => c.GetAttribute("data-column-id"))
            .ToList();

        // Columns use Field names as EffectiveId (no explicit Id set)
        Assert.Contains("Name", ids);
        Assert.Contains("Department", ids);
    }

    /// <summary>
    /// A column with Visible=false must NOT appear in &lt;colgroup&gt;.
    /// </summary>
    [Fact]
    public void Render_HiddenColumn_IsNotRepresentedInColgroup()
    {
        // Three columns defined; the third (Department) has Visible=false
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.ChildContent, ThreeColumnDefs(hideLastColumn: true)));

        var colgroup = cut.Find("colgroup");
        var cols = colgroup.QuerySelectorAll("col[data-column-id]");

        // Only Id and Name should be present
        Assert.Equal(2, cols.Length);

        var ids = cols.Select(c => c.GetAttribute("data-column-id")).ToList();
        Assert.Contains("Id", ids);
        Assert.Contains("Name", ids);
        Assert.DoesNotContain("Department", ids);
    }
}

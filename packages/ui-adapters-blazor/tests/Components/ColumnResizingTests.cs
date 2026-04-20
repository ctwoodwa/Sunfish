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
/// bUnit tests for G37 B2 — SunfishDataGrid column resizing.
/// Covers: AllowColumnResize toggle, per-column Resizable opt-out, min/max attributes,
/// attachGrid options, [JSInvokable] HandleColumnResizedFromJs, GridState.ColumnStates
/// persistence, OnColumnResized EventCallback, and OnStateChanged notification.
/// </summary>
public class ColumnResizingTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record Employee(int Id, string Name, string Department);

    // ── Constructor / DI setup ─────────────────────────────────────────────

    public ColumnResizingTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<IDownloadService, StubDownloadService>();

        // Loose mode: bUnit auto-satisfies import() / attachGrid calls.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Test data helpers ──────────────────────────────────────────────────

    private static List<Employee> TwoEmployees() =>
    [
        new(1, "Alice", "Engineering"),
        new(2, "Bob",   "Design"),
    ];

    private static RenderFragment TwoColumns() => builder =>
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

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.1 + B2.4 — AllowColumnResize=false → no drag handles rendered
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnResize is false (default), no mar-datagrid-col-resize-handle elements
    /// should be rendered in any header cell.
    /// </summary>
    [Fact]
    public void AllowColumnResize_False_NoHandlesRendered()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var handles = cut.FindAll(".mar-datagrid-col-resize-handle");
        Assert.Empty(handles);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.1 + B2.4 — AllowColumnResize=true → drag handles rendered per column
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnResize is true, every resizable column header must contain exactly one
    /// mar-datagrid-col-resize-handle element.
    /// </summary>
    [Fact]
    public void AllowColumnResize_True_HandlesRenderedInEveryResizableHeader()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var handles = cut.FindAll(".mar-datagrid-col-resize-handle");
        // Two data columns, both default Resizable=true → two handles.
        Assert.Equal(2, handles.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.2 — Column Resizable=false → that column gets no handle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When a column sets Resizable=false, its header must NOT contain a resize handle even
    /// when AllowColumnResize is enabled on the grid.
    /// </summary>
    [Fact]
    public void ColumnResizable_False_NoHandleForThatColumn()
    {
        RenderFragment colsWithOneNonResizable = builder =>
        {
            // Name column: resizable (default true)
            builder.OpenComponent<SunfishGridColumn<Employee>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
            builder.CloseComponent();

            // Department column: explicitly non-resizable
            builder.OpenComponent<SunfishGridColumn<Employee>>(3);
            builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
            builder.AddAttribute(5, nameof(SunfishGridColumn<Employee>.Resizable), false);
            builder.CloseComponent();
        };

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, colsWithOneNonResizable));

        var handles = cut.FindAll(".mar-datagrid-col-resize-handle");
        // Only the Name column should have a handle.
        var handle = Assert.Single(handles);

        // The single handle should belong to the Name column.
        Assert.Equal("Name", handle.GetAttribute("data-column-id"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.3 — MinResizableWidth / MaxResizableWidth rendered as data attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MinResizableWidth and MaxResizableWidth must be rendered as data-min-width and
    /// data-max-width attributes on the resize handle element so JS can read them without
    /// a C#↔JS round trip.
    /// </summary>
    [Fact]
    public void MinMaxResizableWidth_RenderedAsDataAttributes()
    {
        RenderFragment colWithConstraints = builder =>
        {
            builder.OpenComponent<SunfishGridColumn<Employee>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
            builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.MinResizableWidth), "80px");
            builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.MaxResizableWidth), "400px");
            builder.CloseComponent();
        };

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, colWithConstraints));

        var handle = cut.Find(".mar-datagrid-col-resize-handle");
        Assert.Equal("80px",  handle.GetAttribute("data-min-width"));
        Assert.Equal("400px", handle.GetAttribute("data-max-width"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2 — attachGrid called with columnResize: true when grid allows resize
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnResize=true, attachGrid must be called on the marilo-datagrid.js module
    /// with an options object where columnResize is true.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenAllowColumnResize_True_PassesColumnResizeTrue()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        // Invocation is a value type — check Identifier instead of null-checking
        Assert.Equal("attachGrid", attachInvocation.Identifier);

        // Third argument is the options object — check columnResize field via reflection
        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var columnResizeVal = options!.GetType().GetProperty("columnResize")?.GetValue(options);
        Assert.True((bool?)columnResizeVal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2 — attachGrid called with columnResize: false when grid does not allow resize
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnResize=false (default), attachGrid must be called with columnResize=false.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenAllowColumnResize_False_PassesColumnResizeFalse()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);

        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var columnResizeVal = options!.GetType().GetProperty("columnResize")?.GetValue(options);
        Assert.False((bool?)columnResizeVal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.6 — [JSInvokable] HandleColumnResizedFromJs dispatches event with correct args
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calling HandleColumnResizedFromJs(0, 250.0) on the grid instance (simulating the JS
    /// mouseup callback) must fire OnColumnResized EventCallback with DataGridColumnResizedEventArgs
    /// carrying the correct index, column id, and width.
    /// </summary>
    [Fact]
    public async Task OnColumnResized_JSInvokable_FiresEventCallbackWithCorrectArgs()
    {
        DataGridColumnResizedEventArgs? captured = null;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnColumnResized,
                EventCallback.Factory.Create<DataGridColumnResizedEventArgs>(
                    this, args => captured = args)));

        // Simulate the JS→.NET call that fires on mouseup.
        await cut.InvokeAsync(() => cut.Instance.HandleColumnResizedFromJs(0, 250.0));

        Assert.NotNull(captured);
        Assert.Equal(0,      captured!.ColumnIndex);
        Assert.Equal(250.0,  captured.NewWidth);
        // ColumnId should match the first visible column's EffectiveId ("Name" from Field).
        Assert.Equal("Name", captured.ColumnId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.7 — GridState.ColumnStates[i].Width updated after OnColumnResized
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After HandleColumnResizedFromJs fires, GetState().ColumnStates must reflect the new width
    /// for the resized column so consumers can persist and restore state.
    /// The width is tracked via column.RuntimeWidth which feeds into EffectiveWidth, which
    /// GetState() reads when building the ColumnStates snapshot.
    /// </summary>
    [Fact]
    public async Task OnColumnResized_UpdatesGridStateColumnStatesWidth()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var grid = cut.Instance;

        await cut.InvokeAsync(() => grid.HandleColumnResizedFromJs(0, 180.0));

        // GetState() builds the ColumnStates snapshot from column.EffectiveWidth (RuntimeWidth ?? Width).
        var snapshot = grid.GetState();
        var colState = snapshot.ColumnStates
            .FirstOrDefault(cs => cs.Field == nameof(Employee.Name));

        Assert.NotNull(colState);
        Assert.Equal("180px", colState!.Width);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.8 — OnColumnResized EventCallback fires with DataGridColumnResizedEventArgs
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// OnColumnResized EventCallback must be invoked with a non-null
    /// DataGridColumnResizedEventArgs when a column is resized.
    /// </summary>
    [Fact]
    public async Task OnColumnResized_EventCallback_IsFiredWithArgs()
    {
        var callCount = 0;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnColumnResized,
                EventCallback.Factory.Create<DataGridColumnResizedEventArgs>(
                    this, _ => callCount++)));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnResizedFromJs(1, 300.0));

        Assert.Equal(1, callCount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.7 — OnStateChanged fires after a resize completes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// OnStateChanged must fire after HandleColumnResizedFromJs processes a resize so consumers
    /// persisting state via OnStateChanged receive the updated column widths.
    /// </summary>
    [Fact]
    public async Task OnColumnResized_FiresOnStateChanged()
    {
        var stateChangedCount = 0;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnStateChanged,
                EventCallback.Factory.Create<GridStateChangedEventArgs>(
                    this, _ => stateChangedCount++)));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnResizedFromJs(0, 200.0));

        Assert.True(stateChangedCount >= 1,
            "Expected OnStateChanged to fire at least once after a column resize.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.4 — Drag handle has correct data-column-index and data-column-id attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The resize handle must carry data-column-index matching the column's position in
    /// _visibleColumns and data-column-id matching the column's EffectiveId.
    /// </summary>
    [Fact]
    public void ResizeHandle_HasCorrectDataAttributes()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var handles = cut.FindAll(".mar-datagrid-col-resize-handle");
        Assert.Equal(2, handles.Count);

        // First handle: Name column at index 0
        Assert.Equal("0",    handles[0].GetAttribute("data-column-index"));
        Assert.Equal("Name", handles[0].GetAttribute("data-column-id"));

        // Second handle: Department column at index 1
        Assert.Equal("1",          handles[1].GetAttribute("data-column-index"));
        Assert.Equal("Department", handles[1].GetAttribute("data-column-id"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B2.2 — IsResizable(bool) helper honours both grid and column flags
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SunfishGridColumn.IsResizable(bool) should return false when either the grid disables
    /// resize or the column opts out, and true only when both allow it.
    /// </summary>
    [Fact]
    public void IsResizable_ReturnsCorrectValue()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var col = cut.Instance._visibleColumns[0]; // Name column, Resizable=true by default

        Assert.True(col.IsResizable(true),   "gridAllows=true, col.Resizable=true  → true");
        Assert.False(col.IsResizable(false),  "gridAllows=false, col.Resizable=true → false");

        // Verify the non-resizable column variant: render a fresh grid with Resizable=false on col
        RenderFragment nonResizableCol = builder =>
        {
            builder.OpenComponent<SunfishGridColumn<Employee>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
            builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Resizable), false);
            builder.CloseComponent();
        };

        var cut2 = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnResize, true)
            .Add(x => x.ChildContent, nonResizableCol));

        var col2 = cut2.Instance._visibleColumns[0]; // Resizable=false
        Assert.False(col2.IsResizable(true),  "gridAllows=true, col.Resizable=false → false");
        Assert.False(col2.IsResizable(false), "gridAllows=false, col.Resizable=false → false");
    }
}

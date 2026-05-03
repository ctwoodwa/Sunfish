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
/// bUnit tests for G37 B4 — SunfishDataGrid row drag-and-drop.
/// Covers: RowDraggable toggle, draggable attribute on rows, data-row-index, drag handle column,
/// attachGrid options, [JSInvokable] HandleRowDroppedFromJs (guard conditions, correct event args,
/// DropPosition parsing, coexistence with AllowColumnReorder).
/// </summary>
public class RowDragDropTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record Employee(int Id, string Name, string Department);

    // ── Constructor / DI setup ─────────────────────────────────────────────

    public RowDragDropTests()
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

    private static List<Employee> ThreeEmployees() =>
    [
        new(1, "Alice", "Engineering"),
        new(2, "Bob",   "Design"),
        new(3, "Carol", "Marketing"),
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
    //  B4.1 — RowDraggable=false → no draggable="true" on rows, no drag handle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When RowDraggable is false (default), no data row should have draggable="true"
    /// and no drag handle column should be rendered.
    /// </summary>
    [Fact]
    public void RowDraggable_False_NoDraggableTrueOnRows_NoDragHandleColumn()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var draggableRows = cut.FindAll("tr[draggable='true']");
        Assert.Empty(draggableRows);

        var dragHandleCells = cut.FindAll("td.sf-datagrid__drag-cell");
        Assert.Empty(dragHandleCells);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.1 + B4.3 — RowDraggable=true → rows have draggable="true" and data-row-index
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When RowDraggable=true, every data row must have draggable="true" and
    /// a sequential data-row-index attribute (0, 1, 2 ...).
    /// </summary>
    [Fact]
    public void RowDraggable_True_RowsHaveDraggableTrueAndDataRowIndex()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var draggableRows = cut.FindAll("tr[draggable='true'][data-row-index]");
        Assert.Equal(3, draggableRows.Count);

        Assert.Equal("0", draggableRows[0].GetAttribute("data-row-index"));
        Assert.Equal("1", draggableRows[1].GetAttribute("data-row-index"));
        Assert.Equal("2", draggableRows[2].GetAttribute("data-row-index"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.3 — RowDraggable=true → drag handle cell renders in each data row
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When RowDraggable=true, every data row must contain a drag handle cell
    /// with class sf-datagrid__drag-cell containing a span.sf-datagrid__row-drag-handle.
    /// </summary>
    [Fact]
    public void RowDraggable_True_DragHandleCellRendersInEachRow()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var dragHandleCells = cut.FindAll("td.sf-datagrid__drag-cell");
        // One drag-handle cell per data row.
        Assert.Equal(3, dragHandleCells.Count);

        // Each drag-handle cell must contain the handle span.
        foreach (var cell in dragHandleCells)
        {
            var handle = cell.QuerySelector("span.sf-datagrid__row-drag-handle");
            Assert.NotNull(handle);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.2 — attachGrid called with rowDragDrop:false when RowDraggable=false
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When RowDraggable=false (default), attachGrid must pass rowDragDrop=false
    /// so the JS installRowDragDrop function is not wired.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenRowDraggable_False_PassesRowDragDropFalse()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);
        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var val = options!.GetType().GetProperty("rowDragDrop")?.GetValue(options);
        Assert.False((bool?)val);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.2 — attachGrid called with rowDragDrop:true when RowDraggable=true
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When RowDraggable=true, attachGrid must pass rowDragDrop=true so
    /// installRowDragDrop is activated in marilo-datagrid.js.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenRowDraggable_True_PassesRowDragDropTrue()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);
        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var val = options!.GetType().GetProperty("rowDragDrop")?.GetValue(options);
        Assert.True((bool?)val);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.4 — OnRowDropped JSInvokable ignores out-of-range source index
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HandleRowDroppedFromJs must silently return (no event fired) when sourceIndex is
    /// out of range, preventing an IndexOutOfRangeException.
    /// </summary>
    [Fact]
    public async Task HandleRowDroppedFromJs_OutOfRangeSourceIndex_NoEventFired()
    {
        GridRowDropEventArgs<Employee>? captured = null;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnRowDrop,
                EventCallback.Factory.Create<GridRowDropEventArgs<Employee>>(
                    this, args => captured = args)));

        await cut.InvokeAsync(() =>
            cut.Instance.HandleRowDroppedFromJs(99, 1, "Before"));

        Assert.Null(captured);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.4 — OnRowDropped JSInvokable ignores source == dest
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HandleRowDroppedFromJs must silently return when source and destination indices
    /// are equal (no-op drop).
    /// </summary>
    [Fact]
    public async Task HandleRowDroppedFromJs_SourceEqualsDestination_NoEventFired()
    {
        GridRowDropEventArgs<Employee>? captured = null;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnRowDrop,
                EventCallback.Factory.Create<GridRowDropEventArgs<Employee>>(
                    this, args => captured = args)));

        await cut.InvokeAsync(() =>
            cut.Instance.HandleRowDroppedFromJs(1, 1, "Before"));

        Assert.Null(captured);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.5 — OnRowDropped fires OnRowDrop with correct GridRowDropEventArgs fields
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HandleRowDroppedFromJs must fire OnRowDrop with correct Item, DestinationItem,
    /// DestinationIndex, and DropPosition fields.
    /// </summary>
    [Fact]
    public async Task HandleRowDroppedFromJs_FiresOnRowDrop_WithCorrectArgs()
    {
        GridRowDropEventArgs<Employee>? captured = null;
        var employees = ThreeEmployees();

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnRowDrop,
                EventCallback.Factory.Create<GridRowDropEventArgs<Employee>>(
                    this, args => captured = args)));

        // Drop row 0 (Alice) onto row 2 (Carol), dropping After.
        await cut.InvokeAsync(() =>
            cut.Instance.HandleRowDroppedFromJs(0, 2, "After"));

        Assert.NotNull(captured);
        Assert.Equal(employees[0], captured!.Item);            // Alice
        Assert.Equal(employees[2], captured.DestinationItem);  // Carol
        Assert.Equal(2, captured.DestinationIndex);
        Assert.Equal(GridRowDropPosition.After, captured.DropPosition);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4.6 — GridRowDropPosition parses "Before" / "After" and falls back to Before
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HandleRowDroppedFromJs must parse "Before" and "After" (case-insensitive) correctly,
    /// and fall back to GridRowDropPosition.Before on garbage input.
    /// </summary>
    [Theory]
    [InlineData("Before", GridRowDropPosition.Before)]
    [InlineData("After",  GridRowDropPosition.After)]
    [InlineData("before", GridRowDropPosition.Before)]
    [InlineData("after",  GridRowDropPosition.After)]
    [InlineData("garbage", GridRowDropPosition.Before)]
    [InlineData("",        GridRowDropPosition.Before)]
    public async Task HandleRowDroppedFromJs_ParsesDropPosition_Correctly(
        string positionString, GridRowDropPosition expected)
    {
        GridRowDropEventArgs<Employee>? captured = null;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnRowDrop,
                EventCallback.Factory.Create<GridRowDropEventArgs<Employee>>(
                    this, args => captured = args)));

        await cut.InvokeAsync(() =>
            cut.Instance.HandleRowDroppedFromJs(0, 1, positionString));

        Assert.NotNull(captured);
        Assert.Equal(expected, captured!.DropPosition);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B4 — Coexistence: AllowColumnReorder=true AND RowDraggable=true
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When both AllowColumnReorder=true and RowDraggable=true, attachGrid must receive
    /// both columnReorder=true and rowDragDrop=true in the options object.
    /// </summary>
    [Fact]
    public void AttachGrid_ColumnReorderAndRowDraggable_BothOptionsTrue()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, ThreeEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.RowDraggable, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);
        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var optType = options!.GetType();

        var colReorder = optType.GetProperty("columnReorder")?.GetValue(options);
        Assert.True((bool?)colReorder, "columnReorder must be true when AllowColumnReorder=true");

        var rowDragDrop = optType.GetProperty("rowDragDrop")?.GetValue(options);
        Assert.True((bool?)rowDragDrop, "rowDragDrop must be true when RowDraggable=true");
    }
}

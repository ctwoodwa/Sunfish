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
/// bUnit tests for G37 B3 — SunfishDataGrid column reordering.
/// Covers: AllowColumnReorder toggle, per-column Reorderable opt-out, data-column-index /
/// data-column-id attributes, draggable attribute emission, attachGrid options,
/// [JSInvokable] HandleColumnReorderingFromJs (cancelable), [JSInvokable]
/// HandleColumnReorderedFromJs (reorder + OrderIndex + GridState.ColumnStates + OnStateChanged).
/// </summary>
public class ColumnReorderingTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record Employee(int Id, string Name, string Department);

    // ── Constructor / DI setup ─────────────────────────────────────────────

    public ColumnReorderingTests()
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

    private static RenderFragment ThreeColumns() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Id));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Id");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(5, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(6);
        builder.AddAttribute(7, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
        builder.AddAttribute(8, nameof(SunfishGridColumn<Employee>.Title), "Department");
        builder.CloseComponent();
    };

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
    //  B3.1 — AllowColumnReorder=false → no draggable="true" on any <th>
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnReorder is false (default), no header cell should have draggable="true".
    /// </summary>
    [Fact]
    public void AllowColumnReorder_False_NoDraggableTrue()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var draggableThs = cut.FindAll("th[draggable='true']");
        Assert.Empty(draggableThs);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.1 + B3.2 — AllowColumnReorder=true + Reorderable=true → draggable="true"
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnReorder=true and all columns have default Reorderable=true,
    /// every data header cell should have draggable="true".
    /// </summary>
    [Fact]
    public void AllowColumnReorder_True_AllColumnsReorderable_DraggableTrueOnAll()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var draggableThs = cut.FindAll("th[draggable='true']");
        // Two data columns, both default Reorderable=true → two draggable headers.
        Assert.Equal(2, draggableThs.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.2 — Column-level Reorderable=false opts that column out
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnReorder=true but a specific column has Reorderable=false,
    /// that column's header should have draggable="false" while others remain draggable="true".
    /// </summary>
    [Fact]
    public void Reorderable_False_OnSpecificColumn_ThatHeaderHasDraggableFalse()
    {
        RenderFragment oneNonReorderable = builder =>
        {
            builder.OpenComponent<SunfishGridColumn<Employee>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
            builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Reorderable), false);
            builder.CloseComponent();

            builder.OpenComponent<SunfishGridColumn<Employee>>(3);
            builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
            // Reorderable defaults to true
            builder.CloseComponent();
        };

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, oneNonReorderable));

        var ths = cut.FindAll("th[data-column-id]");
        // Name column (index 0) should have draggable="false"
        var nameTh = ths.First(th => th.GetAttribute("data-column-id") == "Name");
        Assert.Equal("false", nameTh.GetAttribute("draggable"));

        // Department column (index 1) should have draggable="true"
        var deptTh = ths.First(th => th.GetAttribute("data-column-id") == "Department");
        Assert.Equal("true", deptTh.GetAttribute("draggable"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.3 — data-column-index and data-column-id render correctly
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Every data header cell must carry data-column-index (0-based position in _visibleColumns)
    /// and data-column-id (column EffectiveId — Field when Id not set).
    /// </summary>
    [Fact]
    public void DataColumnAttributes_RenderCorrectly()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var ths = cut.FindAll("th[data-column-index]");
        Assert.Equal(2, ths.Count);

        Assert.Equal("0",    ths[0].GetAttribute("data-column-index"));
        Assert.Equal("Name", ths[0].GetAttribute("data-column-id"));

        Assert.Equal("1",          ths[1].GetAttribute("data-column-index"));
        Assert.Equal("Department", ths[1].GetAttribute("data-column-id"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3 — attachGrid called with columnReorder:true when grid allows reorder
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnReorder=true, attachGrid must be called with columnReorder=true
    /// in the options object passed to marilo-datagrid.js.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenAllowColumnReorder_True_PassesColumnReorderTrue()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);

        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var val = options!.GetType().GetProperty("columnReorder")?.GetValue(options);
        Assert.True((bool?)val);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3 — attachGrid called with columnReorder:false when grid disallows
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When AllowColumnReorder=false (default), attachGrid must pass columnReorder=false.
    /// </summary>
    [Fact]
    public void AttachGrid_WhenAllowColumnReorder_False_PassesColumnReorderFalse()
    {
        var module = JSInterop.SetupModule(
            "./_content/Sunfish.UIAdapters.Blazor/js/marilo-datagrid.js");
        module.Setup<object>("attachGrid", _ => true);

        Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, false)
            .Add(x => x.ChildContent, TwoColumns()));

        var attachInvocation = module.Invocations
            .FirstOrDefault(i => i.Identifier == "attachGrid");

        Assert.Equal("attachGrid", attachInvocation.Identifier);

        var options = attachInvocation.Arguments[2];
        Assert.NotNull(options);
        var val = options!.GetType().GetProperty("columnReorder")?.GetValue(options);
        Assert.False((bool?)val);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.5 — OnColumnReordering JSInvokable returns false for out-of-range index
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HandleColumnReorderingFromJs returns false when oldIndex is out of range,
    /// preventing the reorder from proceeding.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderingFromJs_OutOfRangeIndex_ReturnsFalse()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var result = await cut.InvokeAsync(() =>
            cut.Instance.HandleColumnReorderingFromJs(99, 0));

        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.5 — OnColumnReordering JSInvokable returns false when Cancel=true
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When the consumer's OnColumnReordering handler sets Cancel=true,
    /// HandleColumnReorderingFromJs must return false.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderingFromJs_CancelSetTrue_ReturnsFalse()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnColumnReordering,
                EventCallback.Factory.Create<DataGridColumnReorderingEventArgs>(
                    this, args => args.Cancel = true)));

        var result = await cut.InvokeAsync(() =>
            cut.Instance.HandleColumnReorderingFromJs(0, 1));

        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.5 — OnColumnReordering returns true when not cancelled
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When the consumer's OnColumnReordering handler does not set Cancel,
    /// HandleColumnReorderingFromJs must return true.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderingFromJs_NotCancelled_ReturnsTrue()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var result = await cut.InvokeAsync(() =>
            cut.Instance.HandleColumnReorderingFromJs(0, 1));

        Assert.True(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.6 — OnColumnReordered JSInvokable reorders _columns correctly
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After HandleColumnReorderedFromJs(0, 2) on a 3-column grid the column that was at
    /// index 0 should appear at index 2 in the rendered output header.
    /// Declaration order: Id(0), Name(1), Department(2).
    /// After reorder 0→2: Name(0), Department(1), Id(2).
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderedFromJs_ReordersColumnsCorrectly()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, ThreeColumns()));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnReorderedFromJs(0, 2));

        // After the re-render, check header order via data-column-id attributes.
        var ths = cut.FindAll("th[data-column-id]");
        Assert.Equal("Name",       ths[0].GetAttribute("data-column-id"));
        Assert.Equal("Department", ths[1].GetAttribute("data-column-id"));
        Assert.Equal("Id",         ths[2].GetAttribute("data-column-id"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.6 — OnColumnReordered calls SetOrderIndex so OrderIndex matches position
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After HandleColumnReorderedFromJs, every column's OrderIndex must equal its new
    /// zero-based position in _visibleColumns.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderedFromJs_SetsOrderIndexOnAllColumns()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, ThreeColumns()));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnReorderedFromJs(0, 2));

        var columns = cut.Instance._visibleColumns;
        for (int i = 0; i < columns.Count; i++)
        {
            Assert.Equal(i, columns[i].OrderIndex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.7 — OnColumnReordered updates GridState.ColumnStates[i].Order
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After HandleColumnReorderedFromJs, GetState().ColumnStates must reflect the new Order
    /// values for all columns so consumers can persist and restore column order.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderedFromJs_UpdatesGridStateColumnStatesOrder()
    {
        GridState? capturedState = null;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, ThreeColumns())
            .Add(x => x.OnStateChanged,
                EventCallback.Factory.Create<GridStateChangedEventArgs>(
                    this, args => capturedState = args.State)));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnReorderedFromJs(0, 2));

        Assert.NotNull(capturedState);
        var colStates = capturedState!.ColumnStates;
        Assert.True(colStates.Count >= 3,
            "Expected at least 3 ColumnStates entries after a 3-column reorder.");

        // After moving Id from 0→2: Name=0, Department=1, Id=2
        var nameState = colStates.FirstOrDefault(cs => cs.Field == "Name");
        var deptState = colStates.FirstOrDefault(cs => cs.Field == "Department");
        var idState   = colStates.FirstOrDefault(cs => cs.Field == "Id");

        Assert.NotNull(nameState);
        Assert.NotNull(deptState);
        Assert.NotNull(idState);

        Assert.Equal(0, nameState!.Order);
        Assert.Equal(1, deptState!.Order);
        Assert.Equal(2, idState!.Order);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3 — OnColumnReordered fires OnStateChanged
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// OnStateChanged must fire after HandleColumnReorderedFromJs so consumers persisting
    /// state via OnStateChanged receive the updated column order.
    /// </summary>
    [Fact]
    public async Task HandleColumnReorderedFromJs_FiresOnStateChanged()
    {
        var stateChangedCount = 0;

        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns())
            .Add(x => x.OnStateChanged,
                EventCallback.Factory.Create<GridStateChangedEventArgs>(
                    this, _ => stateChangedCount++)));

        await cut.InvokeAsync(() => cut.Instance.HandleColumnReorderedFromJs(0, 1));

        Assert.True(stateChangedCount >= 1,
            "Expected OnStateChanged to fire at least once after a column reorder.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  B3.2 — IsReorderable helper honours both grid and column flags
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SunfishGridColumn.IsReorderable(bool) returns false when either the grid disables
    /// reorder or the column opts out, and true only when both allow it.
    /// </summary>
    [Fact]
    public void IsReorderable_ReturnsCorrectValue()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, TwoColumns()));

        var col = cut.Instance._visibleColumns[0]; // Name column, Reorderable=true by default

        Assert.True(col.IsReorderable(true),   "gridAllows=true, col.Reorderable=true  → true");
        Assert.False(col.IsReorderable(false),  "gridAllows=false, col.Reorderable=true → false");

        RenderFragment nonReorderableCol = builder =>
        {
            builder.OpenComponent<SunfishGridColumn<Employee>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
            builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Reorderable), false);
            builder.CloseComponent();
        };

        var cut2 = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.AllowColumnReorder, true)
            .Add(x => x.ChildContent, nonReorderableCol));

        var col2 = cut2.Instance._visibleColumns[0]; // Reorderable=false
        Assert.False(col2.IsReorderable(true),  "gridAllows=true, col.Reorderable=false → false");
        Assert.False(col2.IsReorderable(false), "gridAllows=false, col.Reorderable=false → false");
    }
}

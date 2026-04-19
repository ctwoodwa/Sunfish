using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for GridState enrichment (G37 A6.1–A6.6).
/// Covers edit/insert/expand/column state properties on <see cref="GridState"/>
/// and verifies <c>OnStateChanged</c> fires with the enriched snapshot.
/// </summary>
public class GridStateEnrichmentTests : BunitContext
{
    public GridStateEnrichmentTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    // ── A6.1 — EditItem ──────────────────────────────────────────────────────

    /// <summary>A6.1 — EditItem is null when no edit is in progress.</summary>
    [Fact]
    public void GetState_EditItem_IsNullInitially()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        var state = cut.Instance.GetState();
        Assert.Null(state.EditItem);
    }

    /// <summary>A6.1 — EditItem reflects the item currently being edited after BeginEdit.</summary>
    [Fact]
    public async Task GetState_EditItem_ReflectsItemAfterBeginEdit()
    {
        var employees = Employees();
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(employees[0]));

        var state = grid.GetState();
        Assert.NotNull(state.EditItem);
        Assert.Equal(employees[0], state.EditItem);
    }

    /// <summary>A6.1 — EditItem is null after CancelEdit clears the edit session.</summary>
    [Fact]
    public async Task GetState_EditItem_IsNullAfterCancelEdit()
    {
        var employees = Employees();
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(employees[0]));
        Assert.NotNull(grid.GetState().EditItem);

        await cut.InvokeAsync(() => grid.CancelEdit());
        Assert.Null(grid.GetState().EditItem);
    }

    // ── A6.2 — OriginalEditItem ──────────────────────────────────────────────

    /// <summary>A6.2 — OriginalEditItem is captured when BeginEdit is called.</summary>
    [Fact]
    public async Task GetState_OriginalEditItem_CapturedAtBeginEdit()
    {
        var employees = Employees();
        var target = employees[1];
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(target));

        var state = grid.GetState();
        Assert.Equal(target, state.OriginalEditItem);
    }

    /// <summary>A6.2 — OriginalEditItem is cleared after CancelEdit.</summary>
    [Fact]
    public async Task GetState_OriginalEditItem_ClearedOnCancelEdit()
    {
        var employees = Employees();
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(employees[0]));
        Assert.NotNull(grid.GetState().OriginalEditItem);

        await cut.InvokeAsync(() => grid.CancelEdit());
        Assert.Null(grid.GetState().OriginalEditItem);
    }

    // ── A6.3 — InsertedItem ──────────────────────────────────────────────────

    /// <summary>A6.3 — InsertedItem is set after BeginAdd when OnModelInit provides a new item.</summary>
    [Fact]
    public async Task GetState_InsertedItem_ReflectsNewItemAfterBeginAdd()
    {
        var newEmployee = new Employee { Id = 99, Name = "New" };
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef())
            .Add(x => x.OnModelInit, EventCallback.Factory.Create<GridModelInitEventArgs<Employee>>(
                this, args => args.Item = newEmployee)));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginAdd());

        var state = grid.GetState();
        Assert.NotNull(state.InsertedItem);
        Assert.Equal(newEmployee, state.InsertedItem);
    }

    /// <summary>A6.3 — InsertedItem is null when not in add mode.</summary>
    [Fact]
    public void GetState_InsertedItem_IsNullWhenNotInAddMode()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef()));

        Assert.Null(cut.Instance.GetState().InsertedItem);
    }

    // ── A6.4 — ExpandedItems ─────────────────────────────────────────────────

    /// <summary>A6.4 — ExpandedItems gains an entry when a detail row is expanded.</summary>
    [Fact]
    public async Task GetState_ExpandedItems_ContainsItemAfterExpand()
    {
        var employees = Employees();
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.ChildContent, NameColDef())
            .Add(x => x.DetailTemplate, (RenderFragment<Employee>)(item => b =>
            {
                b.OpenElement(0, "span");
                b.AddContent(1, item.Name);
                b.CloseElement();
            })));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.ToggleDetailRow(employees[0]));

        var state = grid.GetState();
        Assert.Contains(employees[0], state.ExpandedItems.Cast<Employee>());
    }

    /// <summary>A6.4 — ExpandedItems removes the entry when detail row is collapsed.</summary>
    [Fact]
    public async Task GetState_ExpandedItems_EmptyAfterCollapse()
    {
        var employees = Employees();
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.ChildContent, NameColDef())
            .Add(x => x.DetailTemplate, (RenderFragment<Employee>)(item => b =>
            {
                b.OpenElement(0, "span");
                b.AddContent(1, item.Name);
                b.CloseElement();
            })));

        var grid = cut.Instance;
        // Expand then collapse
        await cut.InvokeAsync(() => grid.ToggleDetailRow(employees[0]));
        Assert.Single(grid.GetState().ExpandedItems);

        await cut.InvokeAsync(() => grid.ToggleDetailRow(employees[0]));
        Assert.Empty(grid.GetState().ExpandedItems);
    }

    // ── A6.5 — ColumnStates ──────────────────────────────────────────────────

    /// <summary>A6.5 — ColumnStates has one entry per declared visible column.</summary>
    [Fact]
    public void GetState_ColumnStates_HasOneEntryPerDeclaredColumn()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.ChildContent, TwoColDefs()));

        var state = cut.Instance.GetState();
        Assert.Equal(2, state.ColumnStates.Count);
    }

    /// <summary>A6.5 — ColumnStates entries have the correct Field values.</summary>
    [Fact]
    public void GetState_ColumnStates_FieldMatchesDeclaredColumns()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.ChildContent, TwoColDefs()));

        var state = cut.Instance.GetState();
        var fields = state.ColumnStates.Select(c => c.Field).ToList();
        Assert.Contains(nameof(Employee.Name), fields);
        Assert.Contains(nameof(Employee.Department), fields);
    }

    /// <summary>A6.5 — ColumnStates Order reflects declaration order (0-based index).</summary>
    [Fact]
    public void GetState_ColumnStates_OrderReflectsDeclarationOrder()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, Employees())
            .Add(x => x.ChildContent, TwoColDefs()));

        var state = cut.Instance.GetState();
        var nameEntry = state.ColumnStates.FirstOrDefault(c => c.Field == nameof(Employee.Name));
        var deptEntry = state.ColumnStates.FirstOrDefault(c => c.Field == nameof(Employee.Department));
        Assert.NotNull(nameEntry);
        Assert.NotNull(deptEntry);
        Assert.True(nameEntry.Order < deptEntry.Order, "Name column should appear before Department column");
    }

    // ── A6.6 — SearchFilter (regression) ────────────────────────────────────

    /// <summary>A6.6 — SearchFilter in GridState still works as added in A3 (regression check).</summary>
    [Fact]
    public async Task GetState_SearchFilter_ReflectsCurrentSearchText()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, Employees())
            .Add(x => x.ChildContent, NameColDef()));

        var grid = cut.Instance;
        grid.OnSearchChanged(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "alice" });
        await Task.Delay(10);

        var state = grid.GetState();
        Assert.Equal("alice", state.SearchFilter);
    }

    // ── OnStateChanged fires with enriched state ─────────────────────────────

    /// <summary>OnStateChanged fires with a non-null EditItem when BeginEdit is called.</summary>
    [Fact]
    public async Task OnStateChanged_FiresWithEditItemSet_WhenBeginEditCalled()
    {
        var employees = Employees();
        GridStateChangedEventArgs? captured = null;
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef())
            .Add(x => x.OnStateChanged, EventCallback.Factory.Create<GridStateChangedEventArgs>(
                this, args => captured = args)));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(employees[0]));

        Assert.NotNull(captured);
        Assert.Equal("EditItem", captured.PropertyName);
        Assert.Equal(employees[0], captured.State.EditItem);
    }

    /// <summary>OnStateChanged fires with null EditItem after CancelEdit.</summary>
    [Fact]
    public async Task OnStateChanged_FiresWithNullEditItem_WhenCancelEditCalled()
    {
        var employees = Employees();
        GridStateChangedEventArgs? captured = null;
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, employees)
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, NameColDef())
            .Add(x => x.OnStateChanged, EventCallback.Factory.Create<GridStateChangedEventArgs>(
                this, args => captured = args)));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.BeginEdit(employees[0]));
        await cut.InvokeAsync(() => grid.CancelEdit());

        Assert.NotNull(captured);
        Assert.Equal("EditItem", captured.PropertyName);
        Assert.Null(captured.State.EditItem);
        Assert.Null(captured.State.OriginalEditItem);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Employee> Employees() =>
    [
        new() { Id = 1, Name = "Alice", Department = "Engineering" },
        new() { Id = 2, Name = "Bob",   Department = "Marketing" },
        new() { Id = 3, Name = "Carol", Department = "Engineering" },
    ];

    private static RenderFragment NameColDef() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.CloseComponent();
    };

    private static RenderFragment TwoColDefs() => builder =>
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

    private sealed class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Base;
using Sunfish.Components.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for SunfishDataGrid SearchBox (A3.1–A3.6).
/// </summary>
public class SearchBoxTests : BunitContext
{
    public SearchBoxTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    // ── A3.1 / A3.2 — ShowSearchBox parameter ────────────────────────────

    [Fact]
    public void ShowSearchBox_Default_DoesNotRenderSearchInput()
    {
        // Default: ShowSearchBox is false
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, People()));

        Assert.Empty(cut.FindAll("input.sf-datagrid-searchbox-input"));
    }

    [Fact]
    public void ShowSearchBox_True_RendersSearchInput()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.Data, People()));

        var input = cut.Find("input.sf-datagrid-searchbox-input");
        Assert.NotNull(input);
    }

    // ── A3.3 — Global text search ─────────────────────────────────────────

    [Fact]
    public async Task Search_FiltersByStringColumnContains()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        // Directly invoke search (bypass debounce by using delay=0 and awaiting)
        var grid = cut.Instance;
        grid.OnSearchChanged(new ChangeEventArgs { Value = "alice" });
        // Allow microtasks to flush with delay=0
        await Task.Delay(10);
        cut.Render();

        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Alice", rows[0].TextContent);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;
        grid.OnSearchChanged(new ChangeEventArgs { Value = "ALICE" });
        await Task.Delay(10);
        cut.Render();

        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Alice", rows[0].TextContent);
    }

    [Fact]
    public async Task Search_EmptyText_ShowsAllRows()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Apply a filter first
        grid.OnSearchChanged(new ChangeEventArgs { Value = "alice" });
        await Task.Delay(10);
        cut.Render();
        Assert.Single(cut.FindAll("tbody tr"));

        // Now clear it
        grid.OnSearchChanged(new ChangeEventArgs { Value = "" });
        await Task.Delay(10);
        cut.Render();

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public async Task Search_MatchesAcrossMultipleVisibleColumns()
    {
        var data = new List<Person>
        {
            new() { Name = "Alice", City = "Seattle" },
            new() { Name = "Bob",   City = "Phoenix" },
            new() { Name = "Carol", City = "Seattle" },
        };

        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, data)
            .Add(x => x.ChildContent, ColDefsWithCity()));

        var grid = cut.Instance;
        grid.OnSearchChanged(new ChangeEventArgs { Value = "Seattle" });
        await Task.Delay(10);
        cut.Render();

        // Alice and Carol both live in Seattle
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Search_DoesNotMatchHiddenColumns()
    {
        var data = new List<Person>
        {
            new() { Name = "Alice", City = "HiddenCity" },
            new() { Name = "Bob",   City = "OtherCity" },
        };

        // City column is NOT included in ChildContent — only Name is visible
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, data)
            .Add(x => x.ChildContent, ColDefs())); // only Name column

        var grid = cut.Instance;
        // Search for a value that only appears in City (hidden column)
        grid.OnSearchChanged(new ChangeEventArgs { Value = "HiddenCity" });
        await Task.Delay(10);
        cut.Render();

        // No rows should match because City is not a visible column
        var rows = cut.FindAll("tbody tr");
        // The empty-row "No data" cell counts — check it is the empty state
        Assert.Single(rows);
        Assert.Contains("No data", rows[0].TextContent, StringComparison.OrdinalIgnoreCase);
    }

    // ── A3.3 + A3.6 — Search AND's with column filter ────────────────────

    [Fact]
    public async Task Search_AndsWithColumnFilter()
    {
        var data = new List<Person>
        {
            new() { Name = "Alice",  City = "Seattle" },
            new() { Name = "Alicia", City = "Phoenix" },
            new() { Name = "Bob",    City = "Seattle" },
        };

        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, data)
            .Add(x => x.ChildContent, ColDefsWithCity()));

        var grid = cut.Instance;

        // Add a column filter: City == Seattle (must be invoked via dispatcher)
        await cut.InvokeAsync(() => grid.AddFilter(new FilterDescriptor
        {
            Field = nameof(Person.City),
            Operator = FilterOperator.Equals,
            Value = "Seattle"
        }));

        // Add a search: name starts with "Ali"
        // OnSearchChanged is void (debounced), call it inside the dispatcher context
        await cut.InvokeAsync(() => { grid.OnSearchChanged(new ChangeEventArgs { Value = "Ali" }); return Task.CompletedTask; });
        await Task.Delay(10);
        cut.Render();

        // Only Alice matches: City=Seattle AND Name contains "Ali"
        // Alicia has City=Phoenix (fails column filter); Bob has City=Seattle but Name doesn't contain "Ali"
        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Alice", rows[0].TextContent);
    }

    // ── A3.4 — GridState.SearchFilter persistence ────────────────────────

    [Fact]
    public async Task SetStateAsync_WithSearchFilter_AppliesSearch()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Programmatically set state with a SearchFilter value (must be invoked via dispatcher)
        await cut.InvokeAsync(() => grid.SetStateAsync(new GridState { SearchFilter = "bob" }));
        cut.Render();

        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Bob", rows[0].TextContent);
    }

    [Fact]
    public async Task GetState_ReturnsCurrentSearchFilter()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;
        grid.OnSearchChanged(new ChangeEventArgs { Value = "carol" });
        await Task.Delay(10);

        var state = grid.GetState();
        Assert.Equal("carol", state.SearchFilter);
    }

    // ── A3.6 — ClearFilters() clears both column filters and search ───────

    [Fact]
    public async Task ClearFilters_ClearsColumnFiltersAndSearchText()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Add a column filter (must be invoked via dispatcher)
        await cut.InvokeAsync(() => grid.AddFilter(new FilterDescriptor
        {
            Field = nameof(Person.Name),
            Operator = FilterOperator.Contains,
            Value = "Alice"
        }));
        cut.Render();
        Assert.Single(cut.FindAll("tbody tr"));

        // Clear everything (must be invoked via dispatcher)
        await cut.InvokeAsync(() => grid.ClearFilters());
        cut.Render();

        // All 3 rows back
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Equal("", grid._searchText);
    }

    // ── A3.6 — AddFilter programmatic API ─────────────────────────────────

    [Fact]
    public async Task AddFilter_ProgrammaticallyFiltersRows()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Must be invoked via dispatcher because AddFilter calls StateHasChanged
        await cut.InvokeAsync(() => grid.AddFilter(new FilterDescriptor
        {
            Field = nameof(Person.Name),
            Operator = FilterOperator.Equals,
            Value = "Bob"
        }));
        cut.Render();

        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Bob", rows[0].TextContent);
    }

    // ── A3.5 — Debounce respects SearchDelay ─────────────────────────────

    [Fact]
    public async Task SearchDelay_CancelsEarlierKeystrokeWhenTypingFast()
    {
        // Use delay=0 so the debounce fires immediately after we stop typing.
        // Rapid successive keystrokes should cancel the earlier CTS and only the
        // final value is applied.
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 0)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Simulate rapid typing: "b", then "bo", then "bob" — each keystroke cancels prior
        grid.OnSearchChanged(new ChangeEventArgs { Value = "b" });
        grid.OnSearchChanged(new ChangeEventArgs { Value = "bo" });
        grid.OnSearchChanged(new ChangeEventArgs { Value = "bob" });

        // Wait for the (delay=0) debounce timer to fire and InvokeAsync to complete
        await Task.Delay(50);
        cut.Render();

        // Only the final value "bob" should be active — Bob matches
        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("Bob", rows[0].TextContent);

        // And the _searchText reflects the final value
        Assert.Equal("bob", grid._searchText);
    }

    [Fact]
    public async Task SearchDelay_FilterEventuallyAppliesAfterDelay()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.ShowSearchBox, true)
            .Add(x => x.SearchDelay, 150)
            .Add(x => x.Data, People())
            .Add(x => x.ChildContent, ColDefs()));

        var grid = cut.Instance;

        // Trigger search
        grid.OnSearchChanged(new ChangeEventArgs { Value = "alice" });

        // Wait well beyond the debounce delay
        await Task.Delay(400);
        cut.Render();

        // Filter should now be applied
        Assert.Single(cut.FindAll("tbody tr"));
        Assert.Contains("Alice", cut.FindAll("tbody tr")[0].TextContent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<Person> People() =>
    [
        new() { Name = "Alice", City = "Seattle" },
        new() { Name = "Bob",   City = "Portland" },
        new() { Name = "Carol", City = "Spokane" },
    ];

    /// <summary>Renders a single "Name" column only.</summary>
    private static RenderFragment ColDefs() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.CloseComponent();
    };

    /// <summary>Renders "Name" and "City" columns.</summary>
    private static RenderFragment ColDefsWithCity() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Person>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Person>.Field), nameof(Person.City));
        builder.AddAttribute(5, nameof(SunfishGridColumn<Person>.Title), "City");
        builder.CloseComponent();
    };

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for <see cref="SunfishDataGrid{TItem}"/> Size + HighlightedItems (G37 A7).
/// Covers A7.1 (Size parameter), A7.2 (CSS class mapping), A7.3 (HighlightedItems parameter),
/// and A7.4 (sf-datagrid__row--highlighted applied to matching rows).
/// </summary>
public class SizeAndHighlightingTests : BunitContext
{
    public SizeAndHighlightingTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<Sunfish.UIAdapters.Blazor.Internal.Interop.IDownloadService, StubDownloadService>();
    }

    // ── Test model ─────────────────────────────────────────────────────────

    private sealed class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private static List<Widget> ThreeWidgets() =>
    [
        new Widget { Id = 1, Name = "Alpha" },
        new Widget { Id = 2, Name = "Beta" },
        new Widget { Id = 3, Name = "Gamma" },
    ];

    // ── A7.1 + A7.2 — Size parameter and CSS class mapping ────────────────

    /// <summary>
    /// Default Size is Medium → root element carries sf-datagrid--size-medium.
    /// </summary>
    [Fact]
    public void Size_DefaultsMedium_RootHasMediumClass()
    {
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, ThreeWidgets()));

        var grid = cut.Instance;
        Assert.Equal(DataGridSize.Medium, grid.Size);

        var root = cut.Find("[role='grid']");
        Assert.Contains("sf-datagrid--size-medium", root.ClassList);
    }

    /// <summary>
    /// Size=Small → root element carries sf-datagrid--size-small (and NOT medium or large).
    /// </summary>
    [Fact]
    public void Size_Small_RootHasSmallClass()
    {
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, ThreeWidgets())
            .Add(x => x.Size, DataGridSize.Small));

        var root = cut.Find("[role='grid']");
        Assert.Contains("sf-datagrid--size-small", root.ClassList);
        Assert.DoesNotContain("sf-datagrid--size-medium", root.ClassList);
        Assert.DoesNotContain("sf-datagrid--size-large", root.ClassList);
    }

    /// <summary>
    /// Size=Large → root element carries sf-datagrid--size-large (and NOT small or medium).
    /// </summary>
    [Fact]
    public void Size_Large_RootHasLargeClass()
    {
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, ThreeWidgets())
            .Add(x => x.Size, DataGridSize.Large));

        var root = cut.Find("[role='grid']");
        Assert.Contains("sf-datagrid--size-large", root.ClassList);
        Assert.DoesNotContain("sf-datagrid--size-small", root.ClassList);
        Assert.DoesNotContain("sf-datagrid--size-medium", root.ClassList);
    }

    /// <summary>
    /// Existing root classes (from CssProvider + consumer Class parameter) are preserved
    /// alongside the size class — the two don't overwrite each other.
    /// </summary>
    [Fact]
    public void Size_DoesNotRemoveOtherRootClasses()
    {
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, ThreeWidgets())
            .Add(x => x.Size, DataGridSize.Large)
            .Add(x => x.Class, "my-custom-class"));

        var root = cut.Find("[role='grid']");
        // Size class is present
        Assert.Contains("sf-datagrid--size-large", root.ClassList);
        // Consumer-supplied class is also present
        Assert.Contains("my-custom-class", root.ClassList);
    }

    // ── A7.3 + A7.4 — HighlightedItems ────────────────────────────────────

    /// <summary>
    /// HighlightedItems=null → no row has the highlighted CSS class.
    /// </summary>
    [Fact]
    public void HighlightedItems_Null_NoRowHighlighted()
    {
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, ThreeWidgets())
            .Add(x => x.HighlightedItems, (IEnumerable<Widget>?)null));

        var rows = cut.FindAll("tr[role='row']");
        foreach (var row in rows)
        {
            Assert.DoesNotContain("sf-datagrid__row--highlighted", row.ClassList);
        }
    }

    /// <summary>
    /// HighlightedItems=[item1] → exactly the row for item1 has the highlighted class;
    /// the other rows do not. Identity verified by checking which row index (0-based) is
    /// highlighted matches the index of the highlighted item in the data list.
    /// </summary>
    [Fact]
    public void HighlightedItems_SingleItem_OnlyThatRowHighlighted()
    {
        var widgets = ThreeWidgets();
        var toHighlight = widgets[1]; // second item (index 1)

        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, widgets)
            .Add(x => x.HighlightedItems, new[] { toHighlight }));

        // Find all data rows (role=row, inside tbody)
        var dataRows = cut.FindAll("tbody tr[role='row']");
        Assert.Equal(3, dataRows.Count);

        var highlightedRows = dataRows
            .Where(r => r.ClassList.Contains("sf-datagrid__row--highlighted"))
            .ToList();

        // Exactly one row is highlighted
        Assert.Single(highlightedRows);

        // The highlighted row is the second row (index 1), not first (Alpha) or third (Gamma)
        Assert.False(dataRows[0].ClassList.Contains("sf-datagrid__row--highlighted"),
            "First row (Alpha) should not be highlighted");
        Assert.True(dataRows[1].ClassList.Contains("sf-datagrid__row--highlighted"),
            "Second row (Beta) should be highlighted");
        Assert.False(dataRows[2].ClassList.Contains("sf-datagrid__row--highlighted"),
            "Third row (Gamma) should not be highlighted");
    }

    /// <summary>
    /// HighlightedItems with 3 items → all 3 rows highlighted.
    /// </summary>
    [Fact]
    public void HighlightedItems_ThreeItems_AllThreeRowsHighlighted()
    {
        var widgets = ThreeWidgets();

        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, widgets)
            .Add(x => x.HighlightedItems, widgets)); // all three

        var dataRows = cut.FindAll("tbody tr[role='row']");
        Assert.Equal(3, dataRows.Count);

        var highlightedRows = dataRows
            .Where(r => r.ClassList.Contains("sf-datagrid__row--highlighted"))
            .ToList();

        Assert.Equal(3, highlightedRows.Count);
    }

    /// <summary>
    /// Changing HighlightedItems triggers a re-render: the previously highlighted row
    /// loses the class, and the newly highlighted row gains it.
    /// Simulates a parameter change by invoking SetParametersAsync on the component instance.
    /// </summary>
    [Fact]
    public async Task HighlightedItems_Change_TriggersReRenderWithNewHighlights()
    {
        var widgets = ThreeWidgets();

        // Initially highlight Beta (index 1)
        var cut = Render<SunfishDataGrid<Widget>>(p => p
            .Add(x => x.Data, widgets)
            .Add(x => x.HighlightedItems, new[] { widgets[1] }));

        {
            // Initially: row[1] (Beta) is highlighted
            var rows = cut.FindAll("tbody tr[role='row']");
            Assert.Equal(3, rows.Count);
            Assert.True(rows[1].ClassList.Contains("sf-datagrid__row--highlighted"),
                "Row[1] (Beta) should be highlighted initially");
            Assert.False(rows[2].ClassList.Contains("sf-datagrid__row--highlighted"),
                "Row[2] (Gamma) should not be highlighted initially");
        }

        // Simulate parent re-rendering with Gamma (index 2) highlighted instead.
        // SetParametersAsync triggers the full Blazor parameter-set lifecycle including
        // OnParametersSetAsync, which re-materialises _highlightedSet.
        await cut.InvokeAsync(() =>
            cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(SunfishDataGrid<Widget>.Data)] = widgets,
                    [nameof(SunfishDataGrid<Widget>.HighlightedItems)] = (IEnumerable<Widget>)new[] { widgets[2] }
                })));

        {
            var rows = cut.FindAll("tbody tr[role='row']");
            Assert.Equal(3, rows.Count);
            // After change: row[2] (Gamma) is highlighted, row[1] (Beta) is not
            Assert.False(rows[1].ClassList.Contains("sf-datagrid__row--highlighted"),
                "Row[1] (Beta) should no longer be highlighted after parameter change");
            Assert.True(rows[2].ClassList.Contains("sf-datagrid__row--highlighted"),
                "Row[2] (Gamma) should be highlighted after parameter change");
        }
    }
}

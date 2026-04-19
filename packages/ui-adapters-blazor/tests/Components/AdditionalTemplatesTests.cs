using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Data;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

/// <summary>Tests for <see cref="SunfishDataGrid{TItem}"/> A4 template parameters:
/// <see cref="SunfishDataGrid{TItem}.NoDataTemplate"/> and <see cref="SunfishDataGrid{TItem}.RowTemplate"/>.</summary>
public class AdditionalTemplatesTests : BunitContext
{
    public AdditionalTemplatesTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<Sunfish.Components.Blazor.Internal.Interop.IDownloadService, StubDownloadService>();
    }

    private record TestItem(int Id, string Name);

    // ── NoDataTemplate tests ───────────────────────────────────────────────

    /// <summary>A4.1/A4.2 baseline — NoDataTemplate not set + empty items → fallback "No data available." text.</summary>
    [Fact]
    public void NoDataTemplate_NotSet_EmptyItems_ShowsFallbackText()
    {
        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, Enumerable.Empty<TestItem>())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            }));

        var emptyCell = cut.Find("td.sf-datagrid-empty");
        Assert.Contains("No data available.", emptyCell.TextContent);
    }

    /// <summary>A4.2 — NoDataTemplate set + empty items → custom template renders inside a colspan cell.</summary>
    [Fact]
    public void NoDataTemplate_Set_EmptyItems_RendersTemplate()
    {
        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, Enumerable.Empty<TestItem>())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.NoDataTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "id", "custom-empty");
                b.AddContent(2, "Nothing here!");
                b.CloseElement();
            })));

        var emptyCell = cut.Find("td.sf-datagrid-empty");
        Assert.NotNull(emptyCell);

        // Verify the td spans the full column count
        var colspan = emptyCell.GetAttribute("colspan");
        Assert.NotNull(colspan);
        Assert.True(int.Parse(colspan) >= 1);

        // Verify our custom content is rendered inside
        var customSpan = cut.Find("#custom-empty");
        Assert.Equal("Nothing here!", customSpan.TextContent);
    }

    /// <summary>A4.2 — NoDataTemplate set + items present → template does NOT render; rows render normally.</summary>
    [Fact]
    public void NoDataTemplate_Set_ItemsPresent_DoesNotRenderTemplate()
    {
        var items = new[] { new TestItem(1, "Alice"), new TestItem(2, "Bob") };

        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, items)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.NoDataTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "id", "custom-empty");
                b.AddContent(2, "Nothing here!");
                b.CloseElement();
            })));

        // Custom empty template must not be present
        Assert.Empty(cut.FindAll("#custom-empty"));

        // Normal rows should be rendered: tbody should contain data rows
        var tbodyCells = cut.FindAll("tbody td");
        Assert.True(tbodyCells.Count > 0, "Expected data rows to be rendered when items are present.");
    }

    /// <summary>A4.2 — NoDataTemplate + IsLoading=true + empty items → loading overlay shows; template does NOT render.</summary>
    [Fact]
    public void NoDataTemplate_Set_IsLoading_EmptyItems_LoadingWinsTemplateHidden()
    {
        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, Enumerable.Empty<TestItem>())
            .Add(x => x.IsLoading, true)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.NoDataTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "id", "custom-empty");
                b.AddContent(2, "Nothing here!");
                b.CloseElement();
            })));

        // Loading overlay must be visible
        var overlay = cut.Find(".sf-datagrid-loading-overlay");
        Assert.NotNull(overlay);

        // Custom empty template must NOT render while loading
        Assert.Empty(cut.FindAll("#custom-empty"));
    }

    // ── RowTemplate tests ──────────────────────────────────────────────────

    /// <summary>A4.3 baseline — RowTemplate not set → default cell rendering is used.</summary>
    [Fact]
    public void RowTemplate_NotSet_UsesDefaultCellRendering()
    {
        var items = new[] { new TestItem(1, "Alice") };

        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, items)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            }));

        // Default rendering should include a td with "Alice"
        var cells = cut.FindAll("tbody td");
        Assert.True(cells.Any(c => c.TextContent.Contains("Alice")),
            "Expected default rendering to show the Name cell value.");
    }

    /// <summary>A4.4 — RowTemplate set → template renders for each row; default cell text does NOT appear directly.</summary>
    [Fact]
    public void RowTemplate_Set_RendersCustomCells_DefaultCellRenderingNotUsed()
    {
        var items = new[] { new TestItem(1, "Alice"), new TestItem(2, "Bob") };

        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, items)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.RowTemplate, (RenderFragment<TestItem>)(item => b =>
            {
                b.OpenElement(0, "td");
                b.AddAttribute(1, "class", "custom-cell");
                b.AddContent(2, $"Custom:{item.Name}");
                b.CloseElement();
            })));

        // Custom cells should be present
        var customCells = cut.FindAll("td.custom-cell");
        Assert.Equal(2, customCells.Count);
        Assert.Contains(customCells, c => c.TextContent == "Custom:Alice");
        Assert.Contains(customCells, c => c.TextContent == "Custom:Bob");
    }

    /// <summary>A4.4 — RowTemplate receives the correct TItem context (reads item properties).</summary>
    [Fact]
    public void RowTemplate_ReceivesCorrectItemContext()
    {
        var items = new[]
        {
            new TestItem(10, "Charlie"),
            new TestItem(20, "Diana")
        };

        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, items)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.RowTemplate, (RenderFragment<TestItem>)(item => b =>
            {
                b.OpenElement(0, "td");
                b.AddAttribute(1, "data-id", item.Id.ToString());
                b.AddContent(2, item.Name);
                b.CloseElement();
            })));

        var cells = cut.FindAll("tbody td[data-id]");
        Assert.Equal(2, cells.Count);

        var charlieCell = cells.FirstOrDefault(c => c.GetAttribute("data-id") == "10");
        Assert.NotNull(charlieCell);
        Assert.Equal("Charlie", charlieCell!.TextContent);

        var dianaCell = cells.FirstOrDefault(c => c.GetAttribute("data-id") == "20");
        Assert.NotNull(dianaCell);
        Assert.Equal("Diana", dianaCell!.TextContent);
    }

    /// <summary>A4.4 — RowTemplate with OnRowClick — clicking a templated row fires OnRowClick.</summary>
    [Fact]
    public async Task RowTemplate_RowClick_FiresOnRowClick()
    {
        var items = new[] { new TestItem(1, "Alice") };
        GridRowClickEventArgs<TestItem>? receivedArgs = null;

        var cut = Render<SunfishDataGrid<TestItem>>(p => p
            .Add(x => x.Data, items)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<TestItem>>(0);
                builder.AddAttribute(1, "Field", "Name");
                builder.CloseComponent();
            })
            .Add(x => x.RowTemplate, (RenderFragment<TestItem>)(item => b =>
            {
                b.OpenElement(0, "td");
                b.AddAttribute(1, "class", "custom-cell");
                b.AddContent(2, item.Name);
                b.CloseElement();
            }))
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<GridRowClickEventArgs<TestItem>>(
                this, args => { receivedArgs = args; })));

        // Find and click the <tr> row (which the grid renders)
        var row = cut.Find("tbody tr");
        await row.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(receivedArgs);
        Assert.Equal("Alice", receivedArgs!.Item.Name);
    }
}

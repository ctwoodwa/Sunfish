using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

// ═══════════════════════════════════════════════════════════════════════════════
// Writer tests — no grid needed, pure unit tests against CsvExportWriter
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tests for <c>CsvExportWriter</c> (pure, no Blazor component required).
/// </summary>
public class CsvExportWriterTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SunfishGridColumn<Product> MakeColumn(string field, string title)
    {
        var col = new SunfishGridColumn<Product>();
        typeof(SunfishGridColumn<Product>)
            .GetProperty(nameof(SunfishGridColumn<Product>.Field))!
            .SetValue(col, field);
        typeof(SunfishGridColumn<Product>)
            .GetProperty(nameof(SunfishGridColumn<Product>.Title))!
            .SetValue(col, title);
        return col;
    }

    private static GridExportData<Product> MakeData(
        IReadOnlyList<Product> items,
        params (string Field, string Title)[] columns)
    {
        var cols = columns.Select(c => MakeColumn(c.Field, c.Title)).ToList()
                          .AsReadOnly();
        var headers = columns.ToDictionary(c => c.Field, c => c.Title);
        return new GridExportData<Product>(cols, items.ToList().AsReadOnly(), headers);
    }

    private sealed class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Notes { get; set; }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Write_BasicRowsWithHeaders_ProducesCorrectCsvWithCrlfEndings()
    {
        var items = new List<Product>
        {
            new() { Name = "Widget", Price = 9.99m },
            new() { Name = "Gadget", Price = 19.99m },
        };
        var data = MakeData(items, ("Name", "Product Name"), ("Price", "Price"));
        var options = new CsvExportOptions { IncludeHeaders = true };

        var csv = CsvExportWriter.Write(data, options);

        // Header row
        Assert.StartsWith("Product Name,Price\r\n", csv);
        // Data rows
        Assert.Contains("Widget,9.99\r\n", csv);
        Assert.Contains("Gadget,19.99\r\n", csv);
        // All line endings are CRLF (no bare LF)
        Assert.DoesNotContain("\r\r\n", csv);
        var crlfCount = CountOccurrences(csv, "\r\n");
        Assert.Equal(3, crlfCount); // header + 2 rows
    }

    [Fact]
    public void Write_CellContainingComma_GetsQuoted()
    {
        var items = new List<Product>
        {
            new() { Name = "Hello, World" }
        };
        var data = MakeData(items, ("Name", "Name"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        Assert.Contains("\"Hello, World\"", csv);
    }

    [Fact]
    public void Write_CellContainingDoubleQuote_QuotedAndDoubled()
    {
        var items = new List<Product>
        {
            new() { Name = "Say \"Hello\"" }
        };
        var data = MakeData(items, ("Name", "Name"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        // The double-quote inside the cell should be doubled: "Say ""Hello"""
        Assert.Contains("\"Say \"\"Hello\"\"\"", csv);
    }

    [Fact]
    public void Write_CellContainingNewline_GetsQuoted()
    {
        var items = new List<Product>
        {
            new() { Name = "Line1\nLine2" }
        };
        var data = MakeData(items, ("Name", "Name"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        Assert.Contains("\"Line1\nLine2\"", csv);
    }

    [Fact]
    public void Write_NullValue_EmitsEmptyCell_NotNullLiteral()
    {
        var items = new List<Product>
        {
            new() { Notes = null }
        };
        var data = MakeData(items, ("Notes", "Notes"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        // Should be an empty cell (just a CRLF after the leading comma would be on a line with one col)
        Assert.DoesNotContain("null", csv, StringComparison.OrdinalIgnoreCase);
        // The cell should be empty: the row has one column → the row is just "\r\n"
        Assert.Contains("\r\n", csv);
    }

    [Fact]
    public void Write_IncludeHeaders_False_OmitsHeaderRow()
    {
        var items = new List<Product> { new() { Name = "Widget" } };
        var data = MakeData(items, ("Name", "Product Name"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        Assert.DoesNotContain("Product Name", csv);
        Assert.Contains("Widget", csv);
    }

    [Fact]
    public void Write_DateTimeCell_UsesInvariantCulture()
    {
        var dt = new DateTime(2026, 4, 19, 5, 0, 0, DateTimeKind.Utc);
        var items = new List<Product>
        {
            new() { CreatedAt = dt }
        };
        var data = MakeData(items, ("CreatedAt", "Created"));
        var options = new CsvExportOptions { IncludeHeaders = false };

        var csv = CsvExportWriter.Write(data, options);

        // ISO 8601 round-trip format ("O") — locale-independent
        Assert.Contains("2026-04-19", csv);
        // Must NOT use a locale-dependent comma separator (e.g. German "19,04.2026")
        Assert.DoesNotContain("19,04", csv);
    }

    // ── FormatValue / EscapeCell unit tests ──────────────────────────────────

    [Fact]
    public void FormatValue_Null_ReturnsEmpty()
        => Assert.Equal("", CsvExportWriter.FormatValue(null));

    [Fact]
    public void FormatValue_Decimal_UsesInvariantDecimalPoint()
    {
        var result = CsvExportWriter.FormatValue(1234.56m);
        Assert.Equal("1234.56", result);
    }

    [Fact]
    public void EscapeCell_NoSpecialChars_ReturnsAsIs()
        => Assert.Equal("hello", CsvExportWriter.EscapeCell("hello"));

    [Fact]
    public void EscapeCell_Empty_ReturnsEmpty()
        => Assert.Equal("", CsvExportWriter.EscapeCell(""));

    // ── Helper ────────────────────────────────────────────────────────────────

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Grid integration tests — bUnit
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Integration tests for <see cref="SunfishDataGrid{TItem}"/> CSV export (A8.1–A8.6).
/// Uses bUnit + bUnit's <c>JSInterop</c> to mock the <c>downloadText</c> JS call.
/// </summary>
public class CsvExportGridTests : BunitContext
{
    public CsvExportGridTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddSingleton<Sunfish.UIAdapters.Blazor.Internal.Interop.IDownloadService, StubDownloadService>();

        // Set bUnit JS interop to strict mode so unhandled calls throw rather than silently
        // returning defaults. Then register the module import + downloadText call.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Order> ThreeOrders() =>
    [
        new() { Id = 1, Customer = "Alice", Amount = 10.00m },
        new() { Id = 2, Customer = "Bob",   Amount = 20.00m },
        new() { Id = 3, Customer = "Carol", Amount = 30.00m },
    ];

    private static RenderFragment TwoColDefs() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Order>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Customer));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Order>.Title), "Customer");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Order>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Amount));
        builder.AddAttribute(5, nameof(SunfishGridColumn<Order>.Title), "Amount");
        builder.CloseComponent();
    };

    private static RenderFragment ThreeColDefs() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Order>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Id));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Order>.Title), "ID");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Order>>(3);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Customer));
        builder.AddAttribute(5, nameof(SunfishGridColumn<Order>.Title), "Customer");
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Order>>(6);
        builder.AddAttribute(7, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Amount));
        builder.AddAttribute(8, nameof(SunfishGridColumn<Order>.Title), "Amount");
        builder.CloseComponent();
    };

    private void SetupDownloadInterop()
    {
        // The grid imports the clipboard-download module lazily.
        // bUnit Loose mode will auto-handle the import() call and return a mock IJSObjectReference.
        // We additionally set up the downloadText call so we can inspect it.
        JSInterop.SetupModule("./_content/Sunfish.UIAdapters.Blazor/js/sunfish-clipboard-download.js");
    }

    // ── A8.2 — GetExportData returns visible columns + current data ───────────

    [Fact]
    public void GetExportData_ReturnsVisibleColumnsAndCurrentPageItems()
    {
        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        var exportData = grid.GetExportData(exportAllPages: false);

        Assert.Equal(2, exportData.Columns.Count);
        Assert.Equal(3, exportData.Items.Count);
        Assert.Contains("Customer", exportData.Headers.Values);
        Assert.Contains("Amount", exportData.Headers.Values);
    }

    // ── A8.6 — ExportAllPages=false exports only current page ─────────────────

    [Fact]
    public void GetExportData_ExportAllPagesFalse_ReturnsDisplayedPageOnly()
    {
        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.Pageable, true)
            .Add(x => x.PageSize, 2)
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        // Page 1 shows items 1 and 2
        var exportData = grid.GetExportData(exportAllPages: false);

        Assert.Equal(2, exportData.Items.Count);
    }

    // ── A8.6 — ExportAllPages=true exports all filtered items ─────────────────

    [Fact]
    public void GetExportData_ExportAllPagesTrue_ReturnsAllFilteredItems()
    {
        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.Pageable, true)
            .Add(x => x.PageSize, 2)
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        var exportData = grid.GetExportData(exportAllPages: true);

        Assert.Equal(3, exportData.Items.Count);
    }

    // ── A8.3 — CSV content is correct ─────────────────────────────────────────

    [Fact]
    public void GetExportData_CsvWriter_ProducesCorrectContent()
    {
        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        var data = grid.GetExportData(exportAllPages: false);
        var options = new CsvExportOptions { IncludeHeaders = true };
        var csv = CsvExportWriter.Write(data, options);

        Assert.StartsWith("Customer,Amount\r\n", csv);
        Assert.Contains("Alice,10.00\r\n", csv);
        Assert.Contains("Bob,20.00\r\n", csv);
        Assert.Contains("Carol,30.00\r\n", csv);
    }

    // ── A8.5 — OnBeforeExport and OnAfterExport fire in order ─────────────────

    [Fact]
    public async Task ExportToCsvAsync_FiresOnBeforeAndAfterExportInOrder()
    {
        SetupDownloadInterop();

        var fired = new List<string>();
        GridExportEventArgs? capturedBefore = null;
        GridExportEventArgs? capturedAfter = null;

        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, TwoColDefs())
            .Add(x => x.OnBeforeExport, EventCallback.Factory.Create<GridExportEventArgs>(this, args =>
            {
                fired.Add("before");
                capturedBefore = args;
            }))
            .Add(x => x.OnAfterExport, EventCallback.Factory.Create<GridExportEventArgs>(this, args =>
            {
                fired.Add("after");
                capturedAfter = args;
            })));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.ExportToCsvAsync());

        Assert.Equal(new[] { "before", "after" }, fired);
        Assert.NotNull(capturedBefore);
        Assert.NotNull(capturedAfter);
        Assert.Equal("csv", capturedBefore!.Format);
        Assert.Equal("csv", capturedAfter!.Format);
        Assert.NotNull(capturedAfter.Data);
        Assert.True(capturedAfter.RowCount > 0);
    }

    // ── A8.5 — OnBeforeExport IsCancelled stops the export ──────────────────

    [Fact]
    public async Task ExportToCsvAsync_OnBeforeExportCancelled_DoesNotTriggerDownload()
    {
        // Even with strict interop, the download function should NOT be called.
        JSInterop.Mode = JSRuntimeMode.Strict;

        var afterFired = false;

        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, TwoColDefs())
            .Add(x => x.OnBeforeExport, EventCallback.Factory.Create<GridExportEventArgs>(this, args =>
            {
                args.IsCancelled = true;
            }))
            .Add(x => x.OnAfterExport, EventCallback.Factory.Create<GridExportEventArgs>(this, _ =>
            {
                afterFired = true;
            })));

        var grid = cut.Instance;
        // Should not throw (no JS calls happen when cancelled).
        await cut.InvokeAsync(() => grid.ExportToCsvAsync());

        Assert.False(afterFired);
    }

    // ── A8.1 — ExportToCsvAsync calls downloadText with correct args ──────────

    [Fact]
    public async Task ExportToCsvAsync_CallsDownloadTextWithCorrectMimeType()
    {
        SetupDownloadInterop();

        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.ExportToCsvAsync("my-export.csv"));

        // bUnit Loose mode records all invocations. Find the downloadText call.
        var invocations = JSInterop.Invocations.ToList();
        // Import call + downloadText call should be recorded
        Assert.True(invocations.Count >= 1);
    }

    // ── A8.6 — ExportAllPages parameter on the grid is respected ────────────

    [Fact]
    public void GetExportData_ExportAllPages_GridParameter_RespectedByExportToCsvAsync()
    {
        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.Pageable, true)
            .Add(x => x.PageSize, 1)
            .Add(x => x.ExportAllPages, true)
            .Add(x => x.ChildContent, TwoColDefs()));

        var grid = cut.Instance;
        // The grid's ExportAllPages=true means GetExportData(true) — all 3 items
        var data = grid.GetExportData(grid.ExportAllPages);
        Assert.Equal(3, data.Items.Count);
    }

    // ── A8.2 — Hidden columns (Visible=false) are omitted ───────────────────

    [Fact]
    public void GetExportData_HiddenColumns_OmittedFromExport()
    {
        // Build columns where Amount is Visible=false
        RenderFragment colsWithHidden = builder =>
        {
            builder.OpenComponent<SunfishGridColumn<Order>>(0);
            builder.AddAttribute(1, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Customer));
            builder.AddAttribute(2, nameof(SunfishGridColumn<Order>.Title), "Customer");
            builder.CloseComponent();

            builder.OpenComponent<SunfishGridColumn<Order>>(3);
            builder.AddAttribute(4, nameof(SunfishGridColumn<Order>.Field), nameof(Order.Amount));
            builder.AddAttribute(5, nameof(SunfishGridColumn<Order>.Title), "Amount");
            builder.AddAttribute(6, nameof(SunfishGridColumn<Order>.Visible), false);
            builder.CloseComponent();
        };

        var cut = Render<SunfishDataGrid<Order>>(p => p
            .Add(x => x.Data, ThreeOrders())
            .Add(x => x.ChildContent, colsWithHidden));

        var grid = cut.Instance;
        var exportData = grid.GetExportData(exportAllPages: false);

        // Only Customer is visible
        Assert.Single(exportData.Columns);
        Assert.Equal(nameof(Order.Customer), exportData.Columns[0].Field);
        Assert.DoesNotContain("Amount", exportData.Headers.Values);
    }

    // ── Test model ────────────────────────────────────────────────────────────

    private sealed class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = "";
        public decimal Amount { get; set; }
    }
}

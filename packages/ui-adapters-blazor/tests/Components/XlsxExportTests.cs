using Bunit;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

/// <summary>
/// Tests for G37 C1 — SunfishDataGrid Excel export via ClosedXML.
/// Covers both the pure <c>XlsxExportWriter</c> and grid-level integration.
/// </summary>
public class XlsxExportTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record SampleItem(
        int Id,
        string Name,
        decimal Price,
        DateTime CreatedAt,
        bool Active,
        DateOnly? Dob);

    // ── Mocks / shared helpers ─────────────────────────────────────────────

    private readonly IDownloadService _downloadSvc = Substitute.For<IDownloadService>();

    public XlsxExportTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        // Register mock download service so the grid can inject it
        Services.AddScoped<IDownloadService>(_ => _downloadSvc);
        // ISunfishJsModuleLoader is required by the grid's Interop.cs partial
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    // ══════════════════════════════════════════════════════════════════════
    //  XlsxExportWriter — pure unit tests (no Blazor, no bunit)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Writer_BasicRowsAndHeaders_ProducesValidXlsx()
    {
        // Arrange
        var columns = new List<ExportColumnDescriptor>
        {
            new("Id", "ID", null),
            new("Name", "Name", null)
        };
        var items = new List<SampleItem>
        {
            new(1, "Alice", 9.99m, DateTime.Today, true, null),
            new(2, "Bob",   4.49m, DateTime.Today, false, null)
        };
        var options = new XlsxExportOptions { IncludeHeaders = true };

        // Act
        var bytes = XlsxExportWriter.Write(columns, items, options);

        // Assert — round-trip parse
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        Assert.Equal("ID",   ws.Cell(1, 1).GetString());
        Assert.Equal("Name", ws.Cell(1, 2).GetString());
        Assert.Equal("1",    ws.Cell(2, 1).GetString());
        Assert.Equal("Alice",ws.Cell(2, 2).GetString());
        Assert.Equal("2",    ws.Cell(3, 1).GetString());
        Assert.Equal("Bob",  ws.Cell(3, 2).GetString());
    }

    [Fact]
    public void Writer_IncludeHeadersFalse_OmitsHeaderRow()
    {
        var columns = new List<ExportColumnDescriptor> { new("Id", "ID", null) };
        var items = new List<SampleItem> { new(42, "X", 0m, DateTime.Today, true, null) };
        var options = new XlsxExportOptions { IncludeHeaders = false };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // Row 1 should be data, not header
        Assert.Equal("42", ws.Cell(1, 1).GetString());
    }

    [Fact]
    public void Writer_SheetName_AppliedToWorksheet()
    {
        var columns = new List<ExportColumnDescriptor> { new("Id", "ID", null) };
        var items = new List<SampleItem>();
        var options = new XlsxExportOptions { SheetName = "MySheet" };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);

        Assert.Equal("MySheet", wb.Worksheets.First().Name);
    }

    [Fact]
    public void Writer_FreezeHeaderRowTrue_SetsFrozenPanes()
    {
        var columns = new List<ExportColumnDescriptor> { new("Id", "ID", null) };
        var items = new List<SampleItem>();
        var options = new XlsxExportOptions { FreezeHeaderRow = true };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // ClosedXML exposes frozen rows via SheetView
        Assert.Equal(1, ws.SheetView.SplitRow);
    }

    [Fact]
    public void Writer_FormatStringOnColumn_AppliedToDataCells()
    {
        var columns = new List<ExportColumnDescriptor>
        {
            new("Price", "Price", "0.00")
        };
        var items = new List<SampleItem>
        {
            new(1, "A", 12.5m, DateTime.Today, true, null)
        };
        var options = new XlsxExportOptions { IncludeHeaders = true };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        var dataCell = ws.Cell(2, 1); // row 2 = first data row (row 1 = header)
        Assert.Equal("0.00", dataCell.Style.NumberFormat.Format);
    }

    [Fact]
    public void Writer_NullValues_ProduceEmptyCells()
    {
        var columns = new List<ExportColumnDescriptor>
        {
            new("Dob", "Date of Birth", null)
        };
        var items = new List<SampleItem>
        {
            new(1, "A", 0m, DateTime.Today, true, null) // Dob is null
        };
        var options = new XlsxExportOptions { IncludeHeaders = false };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        Assert.True(ws.Cell(1, 1).IsEmpty());
    }

    [Fact]
    public void Writer_DateTimeCell_StoredAsExcelDateTime()
    {
        var columns = new List<ExportColumnDescriptor>
        {
            new("CreatedAt", "Created", null)
        };
        var now = new DateTime(2025, 6, 15, 9, 30, 0, DateTimeKind.Local);
        var items = new List<SampleItem>
        {
            new(1, "A", 0m, now, true, null)
        };
        var options = new XlsxExportOptions { IncludeHeaders = false };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        var cell = ws.Cell(1, 1);
        Assert.Equal(XLDataType.DateTime, cell.DataType);
    }

    [Fact]
    public void Writer_AutoFitColumnsTrue_DoesNotThrow()
    {
        // AutoFitColumns = true calls AdjustToContents(); verify no exception and bytes produced
        var columns = new List<ExportColumnDescriptor> { new("Name", "Name", null) };
        var items = new List<SampleItem> { new(1, "LongNameHere", 0m, DateTime.Today, true, null) };
        var options = new XlsxExportOptions { AutoFitColumns = true };

        var bytes = XlsxExportWriter.Write(columns, items, options);

        Assert.NotEmpty(bytes);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Grid-level integration tests (bunit)
    // ══════════════════════════════════════════════════════════════════════

    private static IReadOnlyList<SampleItem> SampleData => new List<SampleItem>
    {
        new(1, "Alice", 9.99m, new DateTime(2024, 1, 1), true,  new DateOnly(1990, 5, 1)),
        new(2, "Bob",   4.49m, new DateTime(2024, 2, 1), false, new DateOnly(1985, 3, 15)),
        new(3, "Carol", 7.00m, new DateTime(2024, 3, 1), true,  null)
    };

    [Fact]
    public async Task Grid_ExportToExcelAsync_InvokesDownloadServiceWithXlsxMimeType()
    {
        // Arrange
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id")
                .Add(col => col.Title, "ID"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")
                .Add(col => col.Title, "Name")));

        // Act
        await grid.Instance.ExportToExcelAsync();

        // Assert — DownloadService should have been called with xlsx mime type
        await _downloadSvc.Received(1).DownloadAsync(
            Arg.Is<DownloadRequest>(r =>
                r.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" &&
                !string.IsNullOrEmpty(r.Base64Content) &&
                r.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grid_ExportToExcelAsync_Base64DecodesBackToValidXlsx()
    {
        // Arrange
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        DownloadRequest? captured = null;
        await _downloadSvc.DownloadAsync(
            Arg.Do<DownloadRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        // Act
        await grid.Instance.ExportToExcelAsync();

        // Assert
        Assert.NotNull(captured);
        var bytes = Convert.FromBase64String(captured!.Base64Content);
        Assert.True(bytes.Length > 0, "Expected non-empty XLSX bytes");

        // Round-trip parse
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        Assert.Single(wb.Worksheets);
        Assert.Equal("Export", wb.Worksheets.First().Name);
    }

    [Fact]
    public async Task Grid_ExportToExcelAsync_ExportAllPages_IncludesAllItems()
    {
        // Arrange — paged grid showing only 1 item per page
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .Add(g => g.Pageable, true)
            .Add(g => g.PageSize, 1)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        DownloadRequest? captured = null;
        await _downloadSvc.DownloadAsync(
            Arg.Do<DownloadRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        // Act — export all pages
        await grid.Instance.ExportToExcelAsync(new XlsxExportOptions { ExportAllPages = true });

        // Assert — should contain all 3 rows (+ 1 header)
        Assert.NotNull(captured);
        var bytes = Convert.FromBase64String(captured!.Base64Content);
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        // Header row + 3 data rows
        Assert.Equal(4, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Grid_ExportToExcelAsync_OnBeforeExportAndOnAfterExportFire()
    {
        // Arrange
        bool beforeFired = false;
        bool afterFired = false;

        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .Add(g => g.OnBeforeExport, EventCallback.Factory.Create<GridExportEventArgs>(
                this, _ => beforeFired = true))
            .Add(g => g.OnAfterExport, EventCallback.Factory.Create<GridExportEventArgs>(
                this, _ => afterFired = true))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        // Act
        await grid.Instance.ExportToExcelAsync();

        // Assert
        Assert.True(beforeFired, "OnBeforeExport should have fired");
        Assert.True(afterFired,  "OnAfterExport should have fired");
    }

    [Fact]
    public async Task Grid_ExportToExcelAsync_HiddenColumnsOmitted()
    {
        // Arrange
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id")
                .Add(col => col.Title, "ID"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")
                .Add(col => col.Title, "Name")
                .Add(col => col.Visible, false)));   // hidden

        DownloadRequest? captured = null;
        await _downloadSvc.DownloadAsync(
            Arg.Do<DownloadRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        // Act
        await grid.Instance.ExportToExcelAsync();

        // Assert — only 1 column (Id)
        Assert.NotNull(captured);
        var bytes = Convert.FromBase64String(captured!.Base64Content);
        using var stream = new MemoryStream(bytes);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        // Header row should have 1 cell
        Assert.Equal(1, ws.Row(1).LastCellUsed()!.Address.ColumnNumber);
        Assert.Equal("ID", ws.Cell(1, 1).GetString());
        // Column "Name" (index 2) should be empty
        Assert.True(ws.Cell(1, 2).IsEmpty());
    }
}

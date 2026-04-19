using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Components.Blazor.Components.DataDisplay;
using Sunfish.Components.Blazor.Internal.Interop;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

/// <summary>
/// Tests for G37 C2 — SunfishDataGrid PDF export via QuestPDF 2023.12.6 (MIT-pinned).
/// Covers both the pure <c>PdfExportWriter</c> and grid-level integration.
/// </summary>
public class PdfExportTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record SampleItem(
        int Id,
        string Name,
        decimal Price,
        DateTime CreatedAt,
        bool Active);

    // ── Mocks / shared helpers ─────────────────────────────────────────────

    private readonly IDownloadService _downloadSvc = Substitute.For<IDownloadService>();

    public PdfExportTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<IDownloadService>(_ => _downloadSvc);
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    // ── Shared sample data ─────────────────────────────────────────────────

    private static IReadOnlyList<SampleItem> SampleData => new List<SampleItem>
    {
        new(1, "Alice", 9.99m,  new DateTime(2024, 1, 1), true),
        new(2, "Bob",   4.49m,  new DateTime(2024, 2, 1), false),
        new(3, "Carol", 7.00m,  new DateTime(2024, 3, 1), true)
    };

    // ── Column descriptor helper ───────────────────────────────────────────

    private static List<ExportColumnDescriptor> Cols(params (string Field, string Title)[] cols)
        => cols.Select(c => new ExportColumnDescriptor(c.Field, c.Title, null)).ToList();

    // ══════════════════════════════════════════════════════════════════════
    //  PdfExportWriter — pure unit tests (no Blazor, no bunit)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Writer_ProducesNonEmptyByteArray()
    {
        var bytes = PdfExportWriter.Write(Cols(("Id", "ID"), ("Name", "Name")), SampleData, new PdfExportOptions());

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Writer_OutputStartsWithPdfMagicBytes()
    {
        // Every valid PDF starts with "%PDF-" (0x25 0x50 0x44 0x46 0x2D)
        var bytes = PdfExportWriter.Write(Cols(("Id", "ID")), SampleData, new PdfExportOptions());

        Assert.True(bytes.Length >= 5, "PDF should have at least 5 bytes");
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
        Assert.Equal(0x2D, bytes[4]); // '-'
    }

    [Fact]
    public void Writer_IncludeHeadersFalse_ProducesDifferentOutput()
    {
        // Without a full PDF text-extraction oracle, byte-length delta is an adequate proxy:
        // a header row adds cells, so headers-on should produce a different (typically larger)
        // byte sequence than headers-off.
        var columns = Cols(("Id", "ID"), ("Name", "Name"));
        var withHeaders    = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { IncludeHeaders = true });
        var withoutHeaders = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { IncludeHeaders = false });

        Assert.NotEqual(withHeaders.Length, withoutHeaders.Length);
    }

    [Fact]
    public void Writer_LandscapeVsPortrait_ProduceDifferentByteLengths()
    {
        // Different page dimensions → different layout → different byte representation.
        var columns   = Cols(("Id", "ID"), ("Name", "Name"), ("Price", "Price"));
        var portrait  = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { Landscape = false });
        var landscape = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { Landscape = true });

        Assert.NotEqual(portrait.Length, landscape.Length);
    }

    [Fact]
    public void Writer_DocumentTitle_IncreasesOutputSize()
    {
        // A non-empty title adds a header element, which should increase the byte size.
        var columns   = Cols(("Id", "ID"));
        var noTitle   = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { DocumentTitle = null });
        var withTitle = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { DocumentTitle = "My Report Title" });

        Assert.True(withTitle.Length > noTitle.Length,
            "A document with a title should be larger than one without");
    }

    [Fact]
    public void Writer_UnknownPageSize_ThrowsArgumentException()
    {
        var columns = Cols(("Id", "ID"));

        var ex = Assert.Throws<ArgumentException>(() =>
            PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { PageSize = "A3" }));

        Assert.Contains("A3", ex.Message);
    }

    [Theory]
    [InlineData("Letter")]
    [InlineData("A4")]
    [InlineData("Legal")]
    public void Writer_SupportedPageSizes_DoNotThrow(string size)
    {
        var columns = Cols(("Id", "ID"));
        var bytes   = PdfExportWriter.Write(columns, SampleData, new PdfExportOptions { PageSize = size });

        Assert.NotEmpty(bytes);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Grid-level integration tests (bunit)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Grid_ExportToPdfAsync_InvokesDownloadServiceWithPdfMimeType()
    {
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id")
                .Add(col => col.Title, "ID"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")
                .Add(col => col.Title, "Name")));

        await grid.Instance.ExportToPdfAsync();

        await _downloadSvc.Received(1).DownloadAsync(
            Arg.Is<DownloadRequest>(r =>
                r.ContentType == "application/pdf" &&
                !string.IsNullOrEmpty(r.Base64Content) &&
                r.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grid_ExportToPdfAsync_Base64DecodesBackToValidPdf()
    {
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

        await grid.Instance.ExportToPdfAsync();

        Assert.NotNull(captured);
        var bytes = Convert.FromBase64String(captured!.Base64Content);
        Assert.True(bytes.Length > 0, "Expected non-empty PDF bytes");

        // Verify PDF magic bytes (%PDF-)
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
    }

    [Fact]
    public async Task Grid_ExportToPdfAsync_ExportAllPages_IncludesAllItems()
    {
        // Paged grid showing only 1 item per page.
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .Add(g => g.Pageable, true)
            .Add(g => g.PageSize, 1)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        int byteLength = 0;
        await _downloadSvc.DownloadAsync(
            Arg.Do<DownloadRequest>(r => byteLength = Convert.FromBase64String(r.Base64Content).Length),
            Arg.Any<CancellationToken>());

        // Export current page only (1 item)
        await grid.Instance.ExportToPdfAsync(new PdfExportOptions { ExportAllPages = false });
        var singlePageSize = byteLength;

        // Export all pages (3 items)
        await grid.Instance.ExportToPdfAsync(new PdfExportOptions { ExportAllPages = true });
        var allPagesSize = byteLength;

        // More rows → larger PDF
        Assert.True(allPagesSize > singlePageSize,
            "Exporting all pages should produce a larger PDF than a single page");
    }

    [Fact]
    public async Task Grid_ExportToPdfAsync_OnBeforeExportAndOnAfterExportFire()
    {
        bool beforeFired = false;
        bool afterFired  = false;

        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .Add(g => g.OnBeforeExport, EventCallback.Factory.Create<GridExportEventArgs>(
                this, _ => beforeFired = true))
            .Add(g => g.OnAfterExport, EventCallback.Factory.Create<GridExportEventArgs>(
                this, _ => afterFired = true))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        await grid.Instance.ExportToPdfAsync();

        Assert.True(beforeFired, "OnBeforeExport should have fired");
        Assert.True(afterFired,  "OnAfterExport should have fired");
    }

    [Fact]
    public async Task Grid_ExportToPdfAsync_HiddenColumnsOmitted()
    {
        // A grid with one hidden column should produce a PDF that uses only the visible
        // column data. We verify this using PdfExportWriter directly (a pure writer unit test
        // that doesn't require bunit) to prove the column descriptor list is honoured, then
        // verify the grid integration path triggers a non-empty download.

        // ── Pure writer path: 1 column vs 2 columns (large dataset for clear byte-delta) ──
        var bigData = Enumerable.Range(1, 100)
            .Select(i => new SampleItem(i, new string('A', 200), i * 1.5m, DateTime.Today, i % 2 == 0))
            .ToList();

        var oneColDescriptors = new List<ExportColumnDescriptor>
        {
            new("Id", "ID", null)
        };
        var twoColDescriptors = new List<ExportColumnDescriptor>
        {
            new("Id", "ID",     null),
            new("Name", "Name", null)
        };

        var opts = new PdfExportOptions { ExportAllPages = true };
        var bytesOneCol = PdfExportWriter.Write<SampleItem>(oneColDescriptors, bigData, opts);
        var bytesTwoCols = PdfExportWriter.Write<SampleItem>(twoColDescriptors, bigData, opts);

        Assert.True(bytesOneCol.Length > 0, "One-column PDF must not be empty");
        Assert.True(bytesTwoCols.Length > bytesOneCol.Length,
            $"Two-column PDF ({bytesTwoCols.Length} bytes) should be larger than one-column PDF ({bytesOneCol.Length} bytes)");

        // ── Grid integration path: hidden column triggers a download ─────
        var downloadSvc = Substitute.For<IDownloadService>();
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<SunfishOptions>();
        ctx.Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        ctx.Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        ctx.Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        ctx.Services.AddScoped<IDownloadService>(_ => downloadSvc);
        ctx.Services.AddScoped<ISunfishJsModuleLoader>(_ => Substitute.For<ISunfishJsModuleLoader>());

        var grid = ctx.Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id")
                .Add(col => col.Title, "ID"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")
                .Add(col => col.Title, "Name")
                .Add(col => col.Visible, false)));  // hidden — should be omitted

        await grid.Instance.ExportToPdfAsync();

        await downloadSvc.Received(1).DownloadAsync(
            Arg.Is<DownloadRequest>(r =>
                r.ContentType == "application/pdf" &&
                !string.IsNullOrEmpty(r.Base64Content)),
            Arg.Any<CancellationToken>());
    }
}

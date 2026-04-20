using Bunit;
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
/// Tests for SunfishDataGrid PDF export via Playwright.
/// Covers HtmlTableBuilder (pure unit tests, no bunit) and grid-level integration
/// (bunit with a mocked IPdfExportWriter — no live browser required).
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

    // ── Fake PDF bytes — %PDF- magic header ───────────────────────────────

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D];

    // ── Mocks ──────────────────────────────────────────────────────────────

    private readonly IDownloadService _downloadSvc = Substitute.For<IDownloadService>();
    private readonly IPdfExportWriter _pdfWriter   = Substitute.For<IPdfExportWriter>();

    public PdfExportTests()
    {
        _pdfWriter
            .WriteAsync<SampleItem>(
                Arg.Any<IReadOnlyList<ExportColumnDescriptor>>(),
                Arg.Any<IReadOnlyList<SampleItem>>(),
                Arg.Any<PdfExportOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FakePdfBytes));

        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<IDownloadService>(_ => _downloadSvc);
        Services.AddScoped<ISunfishJsModuleLoader>(_ => Substitute.For<ISunfishJsModuleLoader>());
        Services.AddScoped<IPdfExportWriter>(_ => _pdfWriter);
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
    //  HtmlTableBuilder — pure unit tests (no Blazor, no bunit, no browser)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Builder_ProducesHtmlDocument()
    {
        var html = HtmlTableBuilder.Build(Cols(("Id", "ID"), ("Name", "Name")), SampleData, new PdfExportOptions());

        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<table>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_RendersAllDataRows()
    {
        var html = HtmlTableBuilder.Build(Cols(("Name", "Name")), SampleData, new PdfExportOptions());

        Assert.Contains("Alice", html);
        Assert.Contains("Bob",   html);
        Assert.Contains("Carol", html);
    }

    [Fact]
    public void Builder_IncludesColumnHeaders_WhenIncludeHeadersTrue()
    {
        var html = HtmlTableBuilder.Build(
            Cols(("Id", "ID"), ("Name", "Name")),
            SampleData,
            new PdfExportOptions { IncludeHeaders = true });

        Assert.Contains("<th>ID</th>",   html);
        Assert.Contains("<th>Name</th>", html);
    }

    [Fact]
    public void Builder_OmitsHeaderRow_WhenIncludeHeadersFalse()
    {
        var html = HtmlTableBuilder.Build(
            Cols(("Id", "ID"), ("Name", "Name")),
            SampleData,
            new PdfExportOptions { IncludeHeaders = false });

        Assert.DoesNotContain("<th>", html);
    }

    [Fact]
    public void Builder_IncludesDocumentTitle_WhenProvided()
    {
        var html = HtmlTableBuilder.Build(
            Cols(("Id", "ID")),
            SampleData,
            new PdfExportOptions { DocumentTitle = "My Report" });

        Assert.Contains("<h1>", html);
        Assert.Contains("My Report", html);
    }

    [Fact]
    public void Builder_NoTitleElement_WhenDocumentTitleNull()
    {
        var html = HtmlTableBuilder.Build(
            Cols(("Id", "ID")),
            SampleData,
            new PdfExportOptions { DocumentTitle = null });

        Assert.DoesNotContain("<h1>", html);
    }

    [Fact]
    public void Builder_HtmlEncodesSpecialCharacters()
    {
        var items = new List<SampleItem>
        {
            new(1, "<script>alert('xss')</script>", 0, DateTime.Today, true)
        };
        var html = HtmlTableBuilder.Build(Cols(("Name", "Name")), items, new PdfExportOptions());

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Builder_TwoColumns_ProducesMoreContentThanOneColumn()
    {
        var oneCol = HtmlTableBuilder.Build(Cols(("Id", "ID")), SampleData, new PdfExportOptions());
        var twoCols = HtmlTableBuilder.Build(Cols(("Id", "ID"), ("Name", "Name")), SampleData, new PdfExportOptions());

        Assert.True(twoCols.Length > oneCol.Length,
            "Two columns should produce more HTML than one column");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PlaywrightPdfExportWriter.MapFormat — pure validation tests
    // ══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Letter", "Letter")]
    [InlineData("letter", "Letter")]
    [InlineData("A4",     "A4")]
    [InlineData("a4",     "A4")]
    [InlineData("Legal",  "Legal")]
    [InlineData("legal",  "Legal")]
    public void MapFormat_RecognisedSizes_ReturnsCanonicalName(string input, string expected)
    {
        Assert.Equal(expected, PlaywrightPdfExportWriter.MapFormat(input));
    }

    [Fact]
    public void MapFormat_UnknownPageSize_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            PlaywrightPdfExportWriter.MapFormat("A3"));

        Assert.Contains("A3", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Grid-level integration tests (bunit, IPdfExportWriter mocked)
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
    public async Task Grid_ExportToPdfAsync_ExportAllPages_PassesAllItemsToWriter()
    {
        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .Add(g => g.Pageable, true)
            .Add(g => g.PageSize, 1)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")));

        await grid.Instance.ExportToPdfAsync(new PdfExportOptions { ExportAllPages = true });

        await _pdfWriter.Received(1).WriteAsync<SampleItem>(
            Arg.Any<IReadOnlyList<ExportColumnDescriptor>>(),
            Arg.Is<IReadOnlyList<SampleItem>>(items => items.Count == SampleData.Count),
            Arg.Any<PdfExportOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Grid_ExportToPdfAsync_HiddenColumnsOmittedFromDescriptors()
    {
        IReadOnlyList<ExportColumnDescriptor>? capturedCols = null;
        _pdfWriter
            .WriteAsync<SampleItem>(
                Arg.Do<IReadOnlyList<ExportColumnDescriptor>>(cols => capturedCols = cols),
                Arg.Any<IReadOnlyList<SampleItem>>(),
                Arg.Any<PdfExportOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(FakePdfBytes));

        var grid = Render<SunfishDataGrid<SampleItem>>(p => p
            .Add(g => g.Data, SampleData)
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Id")
                .Add(col => col.Title, "ID"))
            .AddChildContent<SunfishGridColumn<SampleItem>>(c => c
                .Add(col => col.Field, "Name")
                .Add(col => col.Title, "Name")
                .Add(col => col.Visible, false)));

        await grid.Instance.ExportToPdfAsync();

        Assert.NotNull(capturedCols);
        Assert.Single(capturedCols);
        Assert.Equal("ID", capturedCols![0].Title);
    }
}

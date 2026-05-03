using Microsoft.Playwright;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

internal sealed class PlaywrightPdfExportWriter : IPdfExportWriter
{
    private readonly SunfishPlaywrightBrowserService _browserService;

    public PlaywrightPdfExportWriter(SunfishPlaywrightBrowserService browserService)
        => _browserService = browserService;

    public async Task<byte[]> WriteAsync<TItem>(
        IReadOnlyList<ExportColumnDescriptor> columns,
        IReadOnlyList<TItem> items,
        PdfExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);

        var format = MapFormat(options.PageSize);
        var html = HtmlTableBuilder.Build(columns, items, options);

        await _browserService.Semaphore.WaitAsync(cancellationToken);
        try
        {
            await using var context = await _browserService.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.SetContentAsync(html, new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.Load
            });
            return await page.PdfAsync(new PagePdfOptions
            {
                Format = format,
                Landscape = options.Landscape,
                PrintBackground = true,
                DisplayHeaderFooter = options.IncludePageNumbers,
                HeaderTemplate = "<div/>",
                FooterTemplate = options.IncludePageNumbers
                    ? """<div style="font-size:8px;width:100%;text-align:center"><span class="pageNumber"></span> / <span class="totalPages"></span></div>"""
                    : "<div/>",
                Margin = new Margin
                {
                    Top = "20px",
                    Bottom = "20px",
                    Left = "20px",
                    Right = "20px"
                },
                Tagged = true,
                Outline = true
            });
        }
        finally
        {
            _browserService.Semaphore.Release();
        }
    }

    internal static string MapFormat(string? pageSize) =>
        pageSize?.Trim() switch
        {
            "Letter" or "letter" => "Letter",
            "A4"     or "a4"     => "A4",
            "Legal"  or "legal"  => "Legal",
            _ => throw new ArgumentException(
                $"Unknown page size '{pageSize}'. Supported values: \"Letter\", \"A4\", \"Legal\".",
                nameof(pageSize))
        };
}

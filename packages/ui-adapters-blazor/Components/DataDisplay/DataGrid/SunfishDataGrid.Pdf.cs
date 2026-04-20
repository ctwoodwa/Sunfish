using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Internal.Interop;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// PDF export logic for <see cref="SunfishDataGrid{TItem}"/>.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    // Resolved lazily so rendering succeeds even without AddSunfishPlaywrightPdf().
    [Inject] private IServiceProvider _pdfServices { get; set; } = default!;
    private IPdfExportWriter? PdfWriter => _pdfServices.GetService<IPdfExportWriter>();

    // ── Public overloads ────────────────────────────────────────────────

    /// <summary>
    /// Exports the current grid view to a PDF file and triggers a browser download.
    /// Uses the grid's <see cref="ExportAllPages"/> parameter to determine whether to
    /// export the current page only or all filtered pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Browser download:</b> triggers a <c>URL.createObjectURL</c>-based download via JS
    /// interop (using the existing <c>download()</c> function in
    /// <c>sunfish-clipboard-download.js</c>). This requires interactive rendering (WebAssembly
    /// or Server with a live circuit). Calling this method during static server-side rendering
    /// (SSR) will throw an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <b>Template-column limitation:</b> columns that use a Blazor <c>Template</c> render
    /// fragment cannot have their rendered output serialised to a PDF cell. The writer falls
    /// back to reflecting on the column's <c>Field</c> property and emitting the raw value
    /// using invariant culture. If the column has no <c>Field</c> either, an empty cell is
    /// emitted.
    /// </para>
    /// <para>
    /// <b>PDF service:</b> PDF export requires <c>AddSunfishPlaywrightPdf()</c> to be called
    /// in your application's DI container. Chromium browser binaries must be installed on the
    /// host machine (<c>playwright install chromium</c>).
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called in a non-interactive (static SSR) context where JS interop is
    /// unavailable. Ensure the component is rendered with <c>InteractiveServer</c> or
    /// <c>InteractiveWebAssembly</c> render mode.
    /// </exception>
    public Task ExportToPdfAsync() =>
        ExportToPdfAsync(new PdfExportOptions
        {
            ExportAllPages = ExportAllPages
        });

    /// <summary>
    /// Exports the current grid view to a PDF file and triggers a browser download.
    /// </summary>
    /// <param name="fileName">
    /// The suggested filename for the browser download. When <c>null</c>, defaults to
    /// <c>grid-export-{timestamp}.pdf</c>.
    /// </param>
    /// <param name="exportAllPages">
    /// When <c>true</c>, exports all items matching active filters and sorts (all pages).
    /// When <c>false</c>, exports only the currently visible page.
    /// </param>
    /// <inheritdoc cref="ExportToPdfAsync()" select="remarks"/>
    public Task ExportToPdfAsync(string? fileName, bool exportAllPages = false) =>
        ExportToPdfAsync(new PdfExportOptions
        {
            FileName = fileName,
            ExportAllPages = exportAllPages
        });

    /// <summary>
    /// Exports the current grid view to a PDF file and triggers a browser download.
    /// </summary>
    /// <param name="options">
    /// Full export options: file name, page size, orientation, title, whether to export all
    /// pages, and whether to include a header row and page-number footer.
    /// When <c>null</c>, defaults are used (current page, headers on, Letter portrait, page
    /// numbers on).
    /// </param>
    /// <inheritdoc cref="ExportToPdfAsync()" select="remarks"/>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="PdfExportOptions.PageSize"/> is not one of
    /// <c>"Letter"</c>, <c>"A4"</c>, or <c>"Legal"</c>.
    /// </exception>
    public async Task ExportToPdfAsync(PdfExportOptions? options = null)
    {
        options ??= new PdfExportOptions { ExportAllPages = ExportAllPages };

        // ── Fire OnBeforeExport ─────────────────────────────────────────
        if (OnBeforeExport.HasDelegate)
        {
            var beforeArgs = new GridExportEventArgs { Format = "pdf" };
            await OnBeforeExport.InvokeAsync(beforeArgs);
            if (beforeArgs.IsCancelled) return;
        }

        // ── Build column descriptors from visible columns ───────────────
        var columns = _visibleColumns
            .Select(c => new ExportColumnDescriptor(c.Field, c.DisplayTitle, c.Format))
            .ToList();

        // ── Resolve data rows ───────────────────────────────────────────
        IEnumerable<TItem> items;
        if (options.ExportAllPages && Data is not null)
        {
            items = Data;
            if (!string.IsNullOrWhiteSpace(_searchText))
                items = ApplySearch(items, _searchText);
            foreach (var filter in _state.FilterDescriptors)
                items = ApplyFilter(items, filter);
            items = ApplySort(items);
        }
        else
        {
            items = _displayedItems;
        }

        var itemList = items.ToList();

        // ── Generate PDF bytes via Playwright ───────────────────────────
        var pdfWriter = PdfWriter
            ?? throw new InvalidOperationException(
                "PDF export requires AddSunfishPlaywrightPdf() in your DI container. " +
                "See SunfishPdfServiceExtensions.");

        var bytes = await pdfWriter.WriteAsync(columns, itemList, options);

        // ── Trigger browser download via IDownloadService ────────────────
        var fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? $"grid-export-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
            : options.FileName;

        await _downloadService.DownloadAsync(new DownloadRequest
        {
            FileName = fileName,
            ContentType = "application/pdf",
            Base64Content = Convert.ToBase64String(bytes)
        });

        // ── Fire OnAfterExport ──────────────────────────────────────────
        if (OnAfterExport.HasDelegate)
        {
            await OnAfterExport.InvokeAsync(new GridExportEventArgs
            {
                Format = "pdf",
                RowCount = itemList.Count
            });
        }
    }
}

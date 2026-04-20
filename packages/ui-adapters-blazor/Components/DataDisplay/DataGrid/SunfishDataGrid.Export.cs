using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// CSV export logic for <see cref="SunfishDataGrid{TItem}"/>.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    // ── JS module reference for download interop ────────────────────────

    /// <summary>
    /// Lazy-loaded reference to <c>sunfish-clipboard-download.js</c>.
    /// Initialised on first export; disposed with the grid.
    /// </summary>
    private IJSObjectReference? _clipboardDownloadModule;

    // ── Public overloads ────────────────────────────────────────────────

    /// <summary>
    /// Exports the current grid view to a CSV file and triggers a browser download.
    /// Uses the grid's <see cref="ExportAllPages"/> parameter to determine whether to
    /// export the current page only or all filtered pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Browser download:</b> triggers a <c>URL.createObjectURL</c>-based download via JS interop.
    /// This requires interactive rendering (WebAssembly or Server with a live circuit). Calling
    /// this method during static server-side rendering (SSR) will throw an
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <b>Custom Template columns:</b> columns that use a Blazor <c>Template</c> render fragment
    /// cannot have their rendered output serialised to plain text. The writer falls back to
    /// reflecting on the column's <c>Field</c> property and emitting the raw value using
    /// invariant culture. If the column has no <c>Field</c> either, an empty cell is emitted.
    /// </para>
    /// <para>
    /// <b>Line endings:</b> the CSV output uses CRLF (<c>\r\n</c>) as mandated by RFC 4180.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called in a non-interactive (static SSR) context where JS interop is
    /// unavailable. Ensure the component is rendered with <c>InteractiveServer</c> or
    /// <c>InteractiveWebAssembly</c> render mode.
    /// </exception>
    public Task ExportToCsvAsync() =>
        ExportToCsvAsync(new CsvExportOptions
        {
            ExportAllPages = ExportAllPages
        });

    /// <summary>
    /// Exports the current grid view to a CSV file and triggers a browser download.
    /// </summary>
    /// <param name="fileName">
    /// The suggested filename for the browser download. When <c>null</c>, defaults to
    /// <c>grid-export-{timestamp}.csv</c>.
    /// </param>
    /// <param name="exportAllPages">
    /// When <c>true</c>, exports all items matching active filters and sorts (all pages).
    /// When <c>false</c>, exports only the currently visible page.
    /// </param>
    /// <inheritdoc cref="ExportToCsvAsync()" select="remarks"/>
    public Task ExportToCsvAsync(string? fileName, bool exportAllPages = false) =>
        ExportToCsvAsync(new CsvExportOptions
        {
            FileName = fileName,
            ExportAllPages = exportAllPages
        });

    /// <summary>
    /// Exports the current grid view to a CSV file and triggers a browser download.
    /// </summary>
    /// <param name="options">
    /// Full export options: file name, whether to export all pages, and whether to include
    /// a header row.
    /// </param>
    /// <inheritdoc cref="ExportToCsvAsync()" select="remarks"/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public async Task ExportToCsvAsync(CsvExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var data = GetExportData(options.ExportAllPages);
        var fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? $"grid-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            : options.FileName;
        const string mimeType = "text/csv;charset=utf-8";

        // Fire OnBeforeExport
        if (OnBeforeExport.HasDelegate)
        {
            var beforeArgs = new GridExportEventArgs { Format = "csv" };
            await OnBeforeExport.InvokeAsync(beforeArgs);
            if (beforeArgs.IsCancelled) return;
        }

        // Generate CSV via the pure writer
        var csv = CsvExportWriter.Write(data, options);

        // Trigger browser download via JS interop
        await TriggerCsvDownloadAsync(fileName, mimeType, csv);

        // Fire OnAfterExport
        if (OnAfterExport.HasDelegate)
        {
            var afterArgs = new GridExportEventArgs
            {
                Format = "csv",
                Data = csv,
                RowCount = data.Items.Count
            };
            await OnAfterExport.InvokeAsync(afterArgs);
        }
    }

    // ── JS download helper ──────────────────────────────────────────────

    /// <summary>
    /// Triggers a browser file download using the <c>sunfish-clipboard-download.js</c> module.
    /// Lazily imports the module on first call.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the JS runtime is not available (static SSR context).
    /// </exception>
    private async Task TriggerCsvDownloadAsync(string fileName, string mimeType, string content)
    {
        try
        {
            _clipboardDownloadModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Sunfish.UIAdapters.Blazor/js/sunfish-clipboard-download.js");

            await _clipboardDownloadModule.InvokeVoidAsync("downloadText", fileName, mimeType, content);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            throw new InvalidOperationException(
                $"{nameof(ExportToCsvAsync)} requires interactive rendering (InteractiveServer or " +
                "InteractiveWebAssembly). CSV export is not available during static server-side rendering (SSR) " +
                "because it requires browser JS interop to trigger the file download.", ex);
        }
        catch (JSDisconnectedException ex)
        {
            throw new InvalidOperationException(
                "The browser connection was lost before the CSV download could be triggered. " +
                "Ensure the component is still connected when calling ExportToCsvAsync.", ex);
        }
    }

    /// <summary>
    /// Disposes the lazily-created clipboard/download JS module reference when the grid is disposed.
    /// Cleanup is invoked from the existing <see cref="DisposeAsync"/> implementation in
    /// <c>SunfishDataGrid.Interop.cs</c>.
    /// </summary>
    internal async Task DisposeExportModuleAsync()
    {
        if (_clipboardDownloadModule is not null)
        {
            try
            {
                await _clipboardDownloadModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            _clipboardDownloadModule = null;
        }
    }
}

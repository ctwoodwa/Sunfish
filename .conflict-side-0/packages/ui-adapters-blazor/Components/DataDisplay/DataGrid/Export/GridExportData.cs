namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// A snapshot of the grid's export-ready data: visible columns, their header labels, and the
/// items to be exported (post-filter, post-sort; optionally all pages or current page only).
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
/// <param name="Columns">The visible columns, in display order.</param>
/// <param name="Items">The items to export, after all active filters and sorts are applied.</param>
/// <param name="Headers">
/// Maps each column's <c>Field</c> name to its display title. Used as the CSV header row.
/// </param>
public sealed record GridExportData<TItem>(
    IReadOnlyList<SunfishGridColumn<TItem>> Columns,
    IReadOnlyList<TItem> Items,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Options controlling how <see cref="SunfishDataGrid{TItem}.ExportToCsvAsync(CsvExportOptions)"/>
/// behaves.
/// </summary>
public sealed record CsvExportOptions
{
    /// <summary>
    /// The filename suggested to the browser. When <c>null</c>, defaults to
    /// <c>grid-export-{timestamp}.csv</c>.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// When <c>true</c>, exports all items that match the active filters and sorts, ignoring
    /// pagination. When <c>false</c> (the default), only the currently visible page is exported.
    /// </summary>
    public bool ExportAllPages { get; init; }

    /// <summary>
    /// Whether to include a header row in the CSV output. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeHeaders { get; init; } = true;
}

/// <summary>
/// Typed event arguments for export lifecycle events
/// (<c>OnBeforeExport</c>, <c>OnAfterExport</c>).
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public sealed class GridExportEventArgs<TItem>
{
    /// <summary>The filename that will be offered to the browser.</summary>
    public string FileName { get; init; } = "";

    /// <summary>The MIME type of the exported file (e.g. <c>text/csv;charset=utf-8</c>).</summary>
    public string MimeType { get; init; } = "text/csv;charset=utf-8";

    /// <summary>The export data snapshot (columns + items + header map).</summary>
    public GridExportData<TItem> Data { get; init; } = default!;
}

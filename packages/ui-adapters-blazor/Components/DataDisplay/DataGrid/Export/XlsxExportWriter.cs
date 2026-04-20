using System.Reflection;
using ClosedXML.Excel;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Pure (no-Blazor-dependency) XLSX writer for <see cref="SunfishDataGrid{TItem}"/> export.
/// Converts a snapshot of visible columns and data rows into a <c>.xlsx</c> byte array
/// using ClosedXML 0.105.x.
/// </summary>
/// <remarks>
/// <para><b>Template-column limitation:</b> columns that rely on a <c>Template</c> render
/// fragment (i.e. have no <c>Field</c> binding) fall back to an empty cell. Use a
/// non-null <c>Field</c> on every column you want populated in the export.</para>
///
/// <para><b>Format strings:</b> ClosedXML format strings follow Excel's number/date format
/// syntax, which is mostly compatible with .NET composite-format strings.  Differences to
/// note:
/// <list type="bullet">
///   <item>Use <c>"yyyy-MM-dd"</c> instead of .NET's <c>"yyyy-MM-dd"</c> — identical here.</item>
///   <item>Currency: use <c>"$#,##0.00"</c> rather than <c>"C2"</c>; Excel does not recognise
///       .NET short-form specifiers.</item>
///   <item>Time zones: <see cref="DateTimeOffset"/> is stored as UTC <see cref="DateTime"/>
///       (offset discarded) — apply formatting accordingly.</item>
/// </list>
/// </para>
/// </remarks>
internal static class XlsxExportWriter
{
    /// <summary>
    /// Generates an <c>.xlsx</c> file from the supplied column/row snapshot and returns its
    /// raw bytes.  The bytes can be transported to the browser via base64 or a stream.
    /// </summary>
    /// <typeparam name="TItem">Row data type.</typeparam>
    /// <param name="columns">
    ///   Visible, ordered column descriptors (field name, display title, format string).
    /// </param>
    /// <param name="items">Data rows to export.</param>
    /// <param name="options">Workbook / worksheet options. Must not be <c>null</c>.</param>
    /// <returns>Raw bytes of the generated <c>.xlsx</c> workbook.</returns>
    public static byte[] Write<TItem>(
        IReadOnlyList<ExportColumnDescriptor> columns,
        IReadOnlyList<TItem> items,
        XlsxExportOptions options)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(options.SheetName);

        int row = 1;

        // ── Header row ──────────────────────────────────────────────────
        if (options.IncludeHeaders)
        {
            for (int c = 0; c < columns.Count; c++)
                ws.Cell(row, c + 1).Value = columns[c].Title;

            ws.Row(row).Style.Font.Bold = true;

            if (options.FreezeHeaderRow)
                ws.SheetView.FreezeRows(1);

            row++;
        }

        // ── Data rows ───────────────────────────────────────────────────
        var propCache = new PropertyInfo?[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            var field = columns[c].Field;
            propCache[c] = string.IsNullOrEmpty(field)
                ? null
                : typeof(TItem).GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            for (int c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                var value = propCache[c]?.GetValue(item);
                SetCellValue(cell, value);
                ApplyFormatString(cell, columns[c].Format);
            }
            row++;
        }

        // ── Column widths ───────────────────────────────────────────────
        if (options.AutoFitColumns)
            ws.Columns().AdjustToContents();

        // ── Serialise ───────────────────────────────────────────────────
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static void SetCellValue(IXLCell cell, object? value)
    {
        if (value is null)
        {
            // Leave cell blank — do not set a value
            return;
        }

        switch (value)
        {
            case bool b:
                cell.Value = b;
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                cell.Value = Convert.ToDouble(value);
                break;
            case float f:
                cell.Value = (double)f;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal dec:
                // ClosedXML stores numeric values as double internally
                cell.Value = (double)dec;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case DateTimeOffset dto:
                // Store as UTC DateTime; offset is discarded
                cell.Value = dto.UtcDateTime;
                break;
            case DateOnly dateOnly:
                cell.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                break;
            case TimeOnly timeOnly:
                // Store as fractional day (Excel time convention)
                cell.Value = timeOnly.ToTimeSpan().TotalDays;
                break;
            case TimeSpan ts:
                cell.Value = ts.TotalDays;
                break;
            case Guid g:
                cell.Value = g.ToString();
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// Applies an Excel number-format string to the cell when <paramref name="format"/> is
    /// non-null and non-empty.  The format string must follow Excel's syntax
    /// (e.g. <c>"0.00"</c>, <c>"yyyy-MM-dd"</c>, <c>"$#,##0.00"</c>).
    /// .NET short-form specifiers such as <c>"C2"</c> or <c>"N0"</c> are NOT supported —
    /// translate them to explicit Excel format strings before passing to this API.
    /// </summary>
    private static void ApplyFormatString(IXLCell cell, string? format)
    {
        if (string.IsNullOrEmpty(format)) return;
        cell.Style.NumberFormat.Format = format;
    }
}

/// <summary>
/// Lightweight column descriptor used by <see cref="XlsxExportWriter"/> to avoid
/// taking a hard dependency on the Blazor <see cref="SunfishGridColumn{TItem}"/> type.
/// </summary>
internal sealed record ExportColumnDescriptor(string? Field, string Title, string? Format);

using System.Globalization;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Pure (no-Blazor-dependency) PDF writer for <see cref="SunfishDataGrid{TItem}"/> export.
/// Converts a <see cref="GridExportData{TItem}"/> snapshot into a <c>.pdf</c> byte array
/// using QuestPDF 2023.12.8 (MIT-licensed; see <c>Directory.Packages.props</c> for the pin
/// rationale).
/// </summary>
/// <remarks>
/// <para>
/// <b>Template-column limitation:</b> columns that rely on a <c>Template</c> render fragment
/// (i.e. have no <c>Field</c> binding) fall back to an empty cell. Use a non-null <c>Field</c>
/// on every column you want populated in the export.
/// </para>
/// <para>
/// <b>Format strings:</b> when a column supplies a <c>Format</c> value, it is applied via
/// <c>string.Format(CultureInfo.InvariantCulture, "{0:format}", value)</c>. If the format
/// string is invalid or incompatible with the value type, the writer falls back silently to
/// <c>Convert.ToString(value, CultureInfo.InvariantCulture)</c>.
/// </para>
/// </remarks>
internal static class PdfExportWriter
{
    /// <summary>
    /// Generates a <c>.pdf</c> file from the supplied column/row snapshot and returns its
    /// raw bytes. The bytes can be transported to the browser via base64 or a stream.
    /// </summary>
    /// <typeparam name="TItem">Row data type.</typeparam>
    /// <param name="data">
    ///   Export snapshot produced by <see cref="SunfishDataGrid{TItem}.GetExportData"/>.
    /// </param>
    /// <param name="options">PDF layout options. Must not be <c>null</c>.</param>
    /// <returns>Raw bytes of the generated PDF document.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <see cref="PdfExportOptions.PageSize"/> is not one of
    ///   <c>"Letter"</c>, <c>"A4"</c>, or <c>"Legal"</c>.
    /// </exception>
    public static byte[] Write<TItem>(GridExportData<TItem> data, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        var pageSize = MapPageSize(options.PageSize, options.Landscape);

        // Build a per-column property cache once — avoids repeated reflection per row.
        var itemType = typeof(TItem);
        var propCache = data.Columns
            .Select(c => string.IsNullOrEmpty(c.Field)
                ? null
                : itemType.GetProperty(c.Field, BindingFlags.Public | BindingFlags.Instance))
            .ToArray();

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(20, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(9));

                // ── Header ────────────────────────────────────────────
                if (!string.IsNullOrEmpty(options.DocumentTitle))
                {
                    page.Header()
                        .PaddingBottom(6)
                        .Text(options.DocumentTitle)
                        .FontSize(14)
                        .Bold();
                }

                // ── Content (table) ───────────────────────────────────
                page.Content().Table(table =>
                {
                    // Equal-width relative columns
                    table.ColumnsDefinition(cols =>
                    {
                        for (int c = 0; c < data.Columns.Count; c++)
                            cols.RelativeColumn();
                    });

                    // Header row
                    if (options.IncludeHeaders)
                    {
                        table.Header(header =>
                        {
                            foreach (var col in data.Columns)
                            {
                                var title = data.Headers.TryGetValue(col.Field ?? "", out var h)
                                    ? h
                                    : col.Field ?? "";

                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(4)
                                    .Text(title)
                                    .Bold();
                            }
                        });
                    }

                    // Data rows
                    foreach (var item in data.Items)
                    {
                        for (int c = 0; c < data.Columns.Count; c++)
                        {
                            var col = data.Columns[c];
                            var text = ResolveCellText(item, propCache[c], col.Format);
                            table.Cell().Padding(4).Text(text);
                        }
                    }
                });

                // ── Footer (page numbers) ────────────────────────────
                if (options.IncludePageNumbers)
                {
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                }
            });
        }).GeneratePdf();

        return bytes;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a logical page-size name and orientation to a QuestPDF <see cref="PageSize"/>.
    /// </summary>
    /// <param name="pageSizeName">Case-insensitive page size: <c>"Letter"</c>, <c>"A4"</c>, <c>"Legal"</c>.</param>
    /// <param name="landscape">When <c>true</c>, returns the landscape variant.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="pageSizeName"/> is not a recognised value.
    /// </exception>
    internal static PageSize MapPageSize(string pageSizeName, bool landscape)
    {
        var size = pageSizeName?.Trim() switch
        {
            "Letter" or "letter" => PageSizes.Letter,
            "A4"     or "a4"     => PageSizes.A4,
            "Legal"  or "legal"  => PageSizes.Legal,
            _ => throw new ArgumentException(
                $"Unknown page size '{pageSizeName}'. Supported values: \"Letter\", \"A4\", \"Legal\".",
                nameof(pageSizeName))
        };

        return landscape ? size.Landscape() : size.Portrait();
    }

    /// <summary>
    /// Resolves the display text for a single table cell. Reflects on <paramref name="prop"/>,
    /// applies <paramref name="format"/> when provided, and falls back gracefully.
    /// </summary>
    internal static string ResolveCellText<TItem>(TItem item, PropertyInfo? prop, string? format)
    {
        if (prop is null) return "";

        var value = prop.GetValue(item);
        if (value is null) return "";

        if (!string.IsNullOrEmpty(format))
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, $"{{0:{format}}}", value);
            }
            catch
            {
                // Format string incompatible with the value type — fall through to default.
            }
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}

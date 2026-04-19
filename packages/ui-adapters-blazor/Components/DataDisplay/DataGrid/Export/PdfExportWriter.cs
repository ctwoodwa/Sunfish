using System.Globalization;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Pure (no-Blazor-dependency) PDF writer for <see cref="SunfishDataGrid{TItem}"/> export.
/// Converts a snapshot of visible columns and data rows into a <c>.pdf</c> byte array
/// using QuestPDF 2023.12.6 (MIT-licensed; see <c>Directory.Packages.props</c> for the pin
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
    // QuestPDF 2023.12.x introduced a runtime license gate that requires this one-time
    // acknowledgement even though the package itself is MIT-licensed at this version.
    // LicenseType.Community is free for individuals, non-profits, and organisations with
    // annual revenue below $1 M USD — see Directory.Packages.props for the full pin note.
    static PdfExportWriter()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    /// <summary>
    /// Generates a <c>.pdf</c> file from the supplied column/row snapshot and returns its
    /// raw bytes. The bytes can be transported to the browser via base64 or a stream.
    /// </summary>
    /// <typeparam name="TItem">Row data type.</typeparam>
    /// <param name="columns">
    ///   Visible, ordered column descriptors (field name, display title, format string).
    /// </param>
    /// <param name="items">Data rows to export.</param>
    /// <param name="options">PDF layout options. Must not be <c>null</c>.</param>
    /// <returns>Raw bytes of the generated PDF document.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <see cref="PdfExportOptions.PageSize"/> is not one of
    ///   <c>"Letter"</c>, <c>"A4"</c>, or <c>"Legal"</c>.
    /// </exception>
    public static byte[] Write<TItem>(
        IReadOnlyList<ExportColumnDescriptor> columns,
        IReadOnlyList<TItem> items,
        PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);

        var pageSize = MapPageSize(options.PageSize, options.Landscape);

        // Build a per-column property cache once — avoids repeated reflection per row.
        var itemType = typeof(TItem);
        var propCache = columns
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
                        for (int c = 0; c < columns.Count; c++)
                            cols.RelativeColumn();
                    });

                    // Header row
                    if (options.IncludeHeaders)
                    {
                        table.Header(header =>
                        {
                            foreach (var col in columns)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(4)
                                    .Text(col.Title)
                                    .Bold();
                            }
                        });
                    }

                    // Data rows
                    foreach (var item in items)
                    {
                        for (int c = 0; c < columns.Count; c++)
                        {
                            var text = ResolveCellText(item, propCache[c], columns[c].Format);
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
    /// <param name="pageSizeName">Page size name: <c>"Letter"</c>, <c>"A4"</c>, <c>"Legal"</c>.</param>
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

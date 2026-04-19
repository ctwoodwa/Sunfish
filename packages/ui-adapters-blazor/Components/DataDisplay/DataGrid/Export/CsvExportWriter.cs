using System.Globalization;
using System.Reflection;
using System.Text;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Pure, stateless CSV writer. Converts a <see cref="GridExportData{TItem}"/> snapshot into an
/// RFC 4180-compliant CSV string.
/// </summary>
/// <remarks>
/// <para>
/// <b>Line endings:</b> RFC 4180 mandates <c>\r\n</c> (CRLF) between records. This writer always
/// uses CRLF regardless of the host platform.
/// </para>
/// <para>
/// <b>Culture:</b> All values are formatted with <see cref="CultureInfo.InvariantCulture"/> so that
/// numbers and dates are unambiguous regardless of the server or client locale. For example,
/// a decimal separator is always <c>.</c>, never <c>,</c>. A <see cref="DateTime"/> is rendered
/// as <c>2026-04-19T05:00:00</c> rather than a locale-specific format.
/// </para>
/// <para>
/// <b>Custom Template columns:</b> when a <see cref="SunfishGridColumn{TItem}"/> has a custom
/// <c>Template</c> render fragment, it is not possible to serialise the rendered Blazor markup
/// into a plain text cell. In that case the writer falls back to reflecting on the column's
/// <c>Field</c> property and formats the raw value. If the column has no <c>Field</c> either,
/// an empty string is emitted.
/// </para>
/// </remarks>
internal static class CsvExportWriter
{
    private const string Crlf = "\r\n";

    /// <summary>
    /// Generates a CSV string from <paramref name="data"/> using <paramref name="options"/>.
    /// </summary>
    /// <typeparam name="TItem">The row data type.</typeparam>
    /// <param name="data">The export snapshot produced by
    /// <see cref="SunfishDataGrid{TItem}.GetExportData"/>.</param>
    /// <param name="options">Export options (header row, file name are consumed by the caller;
    /// <see cref="CsvExportOptions.IncludeHeaders"/> is honoured here).</param>
    /// <returns>An RFC 4180-compliant CSV string with CRLF line endings.</returns>
    internal static string Write<TItem>(GridExportData<TItem> data, CsvExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        var sb = new StringBuilder();

        if (options.IncludeHeaders)
        {
            var headerCells = data.Columns
                .Select(c => EscapeCell(data.Headers.TryGetValue(c.Field ?? "", out var h) ? h : c.Field ?? ""));
            sb.Append(string.Join(",", headerCells));
            sb.Append(Crlf);
        }

        // Cache PropertyInfo lookups per field so we don't repeat reflection per row.
        var propCache = new Dictionary<string, PropertyInfo?>();
        var itemType = typeof(TItem);

        foreach (var item in data.Items)
        {
            var cells = data.Columns.Select(col =>
            {
                var field = col.Field ?? "";
                if (string.IsNullOrEmpty(field)) return "";

                if (!propCache.TryGetValue(field, out var prop))
                {
                    prop = itemType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
                    propCache[field] = prop;
                }

                if (prop is null) return "";

                var value = prop.GetValue(item);
                return EscapeCell(FormatValue(value));
            });

            sb.Append(string.Join(",", cells));
            sb.Append(Crlf);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single cell value to a plain string using invariant culture.
    /// Returns an empty string for <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CultureInfo.InvariantCulture"/> for all
    /// <see cref="IFormattable"/> types (numbers, dates, enums) to ensure that the output
    /// is unambiguous regardless of server locale.
    /// </remarks>
    internal static string FormatValue(object? value)
    {
        if (value is null) return "";

        return value switch
        {
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };
    }

    /// <summary>
    /// Applies RFC 4180 escaping to a single cell value.
    /// <list type="bullet">
    ///   <item>If the value contains <c>,</c>, <c>"</c>, <c>\r</c>, or <c>\n</c> it is wrapped in
    ///   double-quotes.</item>
    ///   <item>Any double-quote inside the value is doubled (<c>""</c>).</item>
    ///   <item>Empty strings and values requiring no escaping are returned as-is.</item>
    /// </list>
    /// </summary>
    internal static string EscapeCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var needsQuoting = value.Contains(',')
                        || value.Contains('"')
                        || value.Contains('\r')
                        || value.Contains('\n');

        if (!needsQuoting) return value;

        // Double any existing double-quotes then wrap in outer quotes.
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

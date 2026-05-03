using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

internal static class HtmlTableBuilder
{
    internal static string Build<TItem>(
        IReadOnlyList<ExportColumnDescriptor> columns,
        IReadOnlyList<TItem> items,
        PdfExportOptions options)
    {
        var itemType = typeof(TItem);
        var propCache = columns
            .Select(c => string.IsNullOrEmpty(c.Field)
                ? null
                : itemType.GetProperty(c.Field, BindingFlags.Public | BindingFlags.Instance))
            .ToArray();

        var sb = new StringBuilder(4096);
        sb.Append("""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="UTF-8">
            <style>
              body  { font-family: Arial, sans-serif; font-size: 9pt; margin: 0; padding: 0; }
              h1    { font-size: 14pt; margin: 0 0 8px 0; }
              table { width: 100%; border-collapse: collapse; table-layout: fixed; }
              th    { background: #e8e8e8; font-weight: bold; padding: 4px; text-align: left; border: 1px solid #ccc; }
              td    { padding: 4px; border: 1px solid #ccc; word-break: break-word; }
            </style>
            </head>
            <body>
            """);

        if (!string.IsNullOrEmpty(options.DocumentTitle))
        {
            sb.Append("<h1>");
            sb.Append(WebUtility.HtmlEncode(options.DocumentTitle));
            sb.Append("</h1>\n");
        }

        sb.Append("<table>\n");

        if (options.IncludeHeaders)
        {
            sb.Append("<thead><tr>");
            foreach (var col in columns)
            {
                sb.Append("<th>");
                sb.Append(WebUtility.HtmlEncode(col.Title));
                sb.Append("</th>");
            }
            sb.Append("</tr></thead>\n");
        }

        sb.Append("<tbody>\n");
        foreach (var item in items)
        {
            sb.Append("<tr>");
            for (int c = 0; c < columns.Count; c++)
            {
                var text = ResolveCellText(item, propCache[c], columns[c].Format);
                sb.Append("<td>");
                sb.Append(WebUtility.HtmlEncode(text));
                sb.Append("</td>");
            }
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</body>\n</html>");
        return sb.ToString();
    }

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

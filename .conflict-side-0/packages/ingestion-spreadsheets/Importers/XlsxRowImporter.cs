using System.Runtime.CompilerServices;
using ClosedXML.Excel;

namespace Sunfish.Ingestion.Spreadsheets.Importers;

/// <summary>
/// XLSX row importer backed by <c>ClosedXML</c>. Reads the first worksheet, treats the first used
/// row as the header row, and emits one dictionary per data row. Cells are read via
/// <see cref="IXLCell.GetString"/> so numbers and dates are surfaced as strings — type coercion
/// happens downstream.
/// </summary>
public sealed class XlsxRowImporter : IRowImporter
{
    /// <inheritdoc />
    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> ReadRowsAsync(
        Stream content, [EnumeratorCancellation] CancellationToken ct)
    {
        // ClosedXML is synchronous; yield once to preserve the async signature.
        await Task.Yield();

        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var rows = sheet.RowsUsed().ToList();
        if (rows.Count == 0) yield break;

        var headerRow = rows[0];
        var headers = headerRow.Cells().Select(c => c.GetString()).ToList();

        for (int i = 1; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var cells = rows[i].Cells(1, headers.Count).ToList();
            var dict = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
            for (int c = 0; c < headers.Count; c++)
                dict[headers[c]] = c < cells.Count ? cells[c].GetString() : string.Empty;
            yield return dict;
        }
    }
}

namespace Sunfish.Ingestion.Spreadsheets.Importers;

/// <summary>
/// Streams raw row dictionaries from a spreadsheet content stream. Implementations are modality-
/// specific (CSV via CsvHelper, XLSX via ClosedXML). Values are always strings at this layer;
/// type coercion is applied downstream by the pipeline.
/// </summary>
public interface IRowImporter
{
    /// <summary>
    /// Reads the content stream and yields one dictionary per data row, keyed by the header row.
    /// </summary>
    /// <param name="content">The spreadsheet content stream.</param>
    /// <param name="ct">Cancellation token honored between rows.</param>
    IAsyncEnumerable<IReadOnlyDictionary<string, string>> ReadRowsAsync(
        Stream content, CancellationToken ct);
}

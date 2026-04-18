using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;

namespace Sunfish.Ingestion.Spreadsheets.Importers;

/// <summary>
/// CSV row importer backed by <c>CsvHelper</c>. Reads the first line as headers and emits one
/// <see cref="Dictionary{TKey, TValue}"/> per data row keyed by header name.
/// </summary>
public sealed class CsvRowImporter : IRowImporter
{
    /// <inheritdoc />
    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> ReadRowsAsync(
        Stream content, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) { DetectDelimiter = false };
        using var csv = new CsvReader(reader, cfg);

        if (!await csv.ReadAsync().ConfigureAwait(false))
            yield break;

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>(headers.Length, StringComparer.Ordinal);
            foreach (var h in headers)
                row[h] = csv.GetField(h) ?? string.Empty;
            yield return row;
        }
    }
}

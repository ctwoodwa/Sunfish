using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Spreadsheets.Coercion;
using Sunfish.Ingestion.Spreadsheets.Importers;

namespace Sunfish.Ingestion.Spreadsheets;

/// <summary>
/// Batch-per-file ingestion pipeline for spreadsheets. Produces a single
/// <c>bulk_import_session</c> <see cref="IngestedEntity"/> and one <c>entity.created</c>
/// <see cref="IngestedEvent"/> per successfully coerced row. Any row-level error fails the
/// whole batch so partial imports cannot corrupt downstream state. See spec §7.2.
/// </summary>
public sealed class SpreadsheetIngestionPipeline : IIngestionPipeline<SpreadsheetUpload>
{
    private readonly CsvRowImporter _csv;
    private readonly XlsxRowImporter _xlsx;

    /// <summary>Creates a pipeline using the provided per-format importers.</summary>
    public SpreadsheetIngestionPipeline(CsvRowImporter csv, XlsxRowImporter xlsx)
    {
        _csv = csv;
        _xlsx = xlsx;
    }

    /// <inheritdoc />
    public async ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        SpreadsheetUpload input, IngestionContext context, CancellationToken ct = default)
    {
        IRowImporter importer = input.Kind == SpreadsheetKind.Csv
            ? (IRowImporter)_csv
            : _xlsx;

        var events = new List<IngestedEvent>();
        var errors = new List<string>();
        var rowIndex = 0;
        var nowUtc = DateTime.UtcNow;

        await foreach (var row in importer.ReadRowsAsync(input.Content, ct).ConfigureAwait(false))
        {
            rowIndex++;
            var body = new Dictionary<string, object?>(StringComparer.Ordinal);
            var rowOk = true;
            foreach (var mapping in input.Mappings)
            {
                if (!row.TryGetValue(mapping.SourceHeader, out var raw))
                {
                    errors.Add($"Row {rowIndex}: missing column '{mapping.SourceHeader}'.");
                    rowOk = false;
                    continue;
                }
                var coerced = TypeCoercer.TryCoerce(raw, mapping.TypeCoercion);
                if (!coerced.IsSuccess)
                {
                    errors.Add($"Row {rowIndex} field '{mapping.TargetField}': {coerced.Failure!.Message}");
                    rowOk = false;
                    continue;
                }
                body[mapping.TargetField] = coerced.Value;
            }
            if (rowOk)
                events.Add(new IngestedEvent("entity.created", body, nowUtc));
        }

        if (errors.Count > 0)
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.ValidationFailed,
                $"Import has {errors.Count} validation error(s); batch not committed.", errors);

        var sessionBody = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["filename"] = input.Filename,
            ["kind"] = input.Kind.ToString(),
            ["rowCount"] = events.Count,
            ["schemaId"] = input.SchemaId,
        };

        var session = new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: "sunfish.ingestion.bulk_import_session/1",
            Body: sessionBody,
            Events: events,
            BlobCids: Array.Empty<Sunfish.Foundation.Blobs.Cid>());

        return IngestionResult<IngestedEntity>.Success(session);
    }
}

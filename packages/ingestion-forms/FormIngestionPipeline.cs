using System.Text.Json;
using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Forms;

/// <summary>
/// Converts a <see cref="FormSubmission{TModel}"/> into an <see cref="IngestedEntity"/>
/// whose body is the serialized model. Form data is always small and structured; no
/// blob-store routing (per spec §7.1 + D-BLOB-BOUNDARY — forms stay inline).
/// </summary>
public sealed class FormIngestionPipeline<TModel> : IIngestionPipeline<FormSubmission<TModel>>
    where TModel : class
{
    public ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        FormSubmission<TModel> input,
        IngestionContext context,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToElement(input.Model);
        var body = json.ValueKind == JsonValueKind.Object
            ? json.EnumerateObject().ToDictionary(
                p => p.Name,
                p => (object?)p.Value.Clone())
            : new Dictionary<string, object?>();

        var entity = new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: new[] { new IngestedEvent("entity.created", body, input.SubmittedAtUtc) },
            BlobCids: Array.Empty<Sunfish.Foundation.Blobs.Cid>());

        return ValueTask.FromResult(IngestionResult<IngestedEntity>.Success(entity));
    }
}

namespace Sunfish.Ingestion.Core;

/// <summary>
/// Contract for a modality-specific ingestion pipeline that normalizes arbitrary external input
/// (<typeparamref name="TInput"/>) into a kernel-shaped <see cref="IngestedEntity"/> with zero or
/// more associated <see cref="IngestedEvent"/> records. See Sunfish Platform spec §7.7.
/// </summary>
/// <typeparam name="TInput">The modality-specific input type (e.g. form submission, spreadsheet row, sensor frame).</typeparam>
public interface IIngestionPipeline<TInput>
{
    /// <summary>
    /// Runs the pipeline for a single input and returns an <see cref="IngestionResult{T}"/> that
    /// carries either the normalized entity or a structured failure.
    /// </summary>
    /// <param name="input">The modality-specific input payload.</param>
    /// <param name="context">Ambient tenant/actor/correlation context for the call.</param>
    /// <param name="ct">Cancellation token propagated through middleware and handlers.</param>
    ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        TInput input,
        IngestionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// A normalized entity produced by an ingestion pipeline. Downstream kernel consumers receive
/// <c>(entity, event[])</c> tuples derived from these records — see Sunfish Platform spec §7.7.
/// </summary>
/// <param name="EntityId">Stable domain identifier for the entity (not a database key).</param>
/// <param name="SchemaId">Schema identifier describing the shape of <paramref name="Body"/>.</param>
/// <param name="Body">Free-form structured payload matching <paramref name="SchemaId"/>.</param>
/// <param name="Events">Ordered list of domain events emitted during ingestion.</param>
/// <param name="BlobCids">Content-addressed identifiers of any blobs captured during ingestion.</param>
public sealed record IngestedEntity(
    string EntityId,
    string SchemaId,
    IReadOnlyDictionary<string, object?> Body,
    IReadOnlyList<IngestedEvent> Events,
    IReadOnlyList<Sunfish.Foundation.Blobs.Cid> BlobCids);

/// <summary>
/// A domain event emitted alongside an <see cref="IngestedEntity"/> — e.g. "FormSubmitted",
/// "SensorReadingCaptured". Consumed by downstream kernel projectors per spec §7.7.
/// </summary>
/// <param name="Kind">The event kind (domain-defined string).</param>
/// <param name="Payload">Event payload as a free-form dictionary.</param>
/// <param name="OccurredUtc">UTC instant at which the event is considered to have occurred.</param>
public sealed record IngestedEvent(
    string Kind,
    IReadOnlyDictionary<string, object?> Payload,
    DateTime OccurredUtc);

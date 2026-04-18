namespace Sunfish.Ingestion.Imagery;

/// <summary>
/// Input to <see cref="ImageryIngestionPipeline"/> — a single image upload with its stream content,
/// filename, content type, and the schema id to stamp on the resulting <c>IngestedEntity</c>.
/// See Sunfish Platform spec §7.5.
/// </summary>
/// <param name="Content">The raw image bytes as a readable stream.</param>
/// <param name="Filename">Original filename (display / debugging use only — not persisted as identity).</param>
/// <param name="ContentType">IANA media type (e.g. <c>image/jpeg</c>).</param>
/// <param name="SchemaId">Schema id to stamp on the resulting entity.</param>
public sealed record ImageUpload(
    Stream Content,
    string Filename,
    string ContentType,
    string SchemaId);

namespace Sunfish.Ingestion.Voice;

/// <summary>
/// An audio payload presented to the voice ingestion pipeline. The stream is read once by the
/// pipeline (buffered to bytes) so callers may close or dispose the original source after the
/// ingestion call returns.
/// </summary>
/// <param name="Content">The raw audio bytes as a readable stream.</param>
/// <param name="Filename">Original filename — used as the <c>filename</c> part in multipart uploads.</param>
/// <param name="ContentType">MIME content type (e.g. <c>audio/wav</c>, <c>audio/mpeg</c>).</param>
/// <param name="SchemaId">Schema identifier the resulting entity will carry.</param>
public sealed record AudioBlob(
    Stream Content,
    string Filename,
    string ContentType,
    string SchemaId);

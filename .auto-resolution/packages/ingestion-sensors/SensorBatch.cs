namespace Sunfish.Ingestion.Sensors;

/// <summary>
/// A batch of sensor readings presented to the sensor ingestion pipeline. The stream is read
/// once by the pipeline (buffered for blob archival) so callers may close or dispose the
/// original source after the ingestion call returns.
/// </summary>
/// <param name="Content">Raw batch bytes as a readable stream (JSON array, NDJSON, or MessagePack).</param>
/// <param name="Format">Encoding that tells the pipeline which decoder to select.</param>
/// <param name="ProducerId">Identifier of the sensor gateway or producer that emitted the batch.</param>
/// <param name="SchemaId">Schema identifier the resulting entity will carry.</param>
public sealed record SensorBatch(
    Stream Content,
    SensorBatchFormat Format,
    string ProducerId,
    string SchemaId);

/// <summary>
/// Supported encodings for a <see cref="SensorBatch"/> payload.
/// </summary>
public enum SensorBatchFormat
{
    /// <summary>JSON array of reading objects.</summary>
    Json,

    /// <summary>Newline-delimited JSON — one reading object per line.</summary>
    JsonNdjson,

    /// <summary>MessagePack-encoded batch (Phase-C parking-lot item — decoder throws).</summary>
    MessagePack,
}

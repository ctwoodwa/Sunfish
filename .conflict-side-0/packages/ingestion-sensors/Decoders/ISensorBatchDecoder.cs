namespace Sunfish.Ingestion.Sensors.Decoders;

/// <summary>
/// Streams <see cref="SensorReading"/> records out of a batch payload. Implementations must yield
/// readings incrementally (one-at-a-time via <c>await foreach</c>) rather than returning a
/// materialized list up front.
/// </summary>
public interface ISensorBatchDecoder
{
    /// <summary>
    /// Decodes readings from <paramref name="content"/>, yielding them in source order. Throws
    /// format-specific exceptions (e.g. <see cref="System.Text.Json.JsonException"/>) when the
    /// stream cannot be parsed; the pipeline maps those to structured failures.
    /// </summary>
    IAsyncEnumerable<SensorReading> DecodeAsync(Stream content, CancellationToken ct);
}

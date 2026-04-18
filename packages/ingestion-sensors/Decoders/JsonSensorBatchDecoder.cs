using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Sunfish.Ingestion.Sensors.Decoders;

/// <summary>
/// JSON decoder that handles both JSON-array-of-objects and NDJSON (newline-delimited). The
/// format is specified at construction time via the <see cref="SensorBatchFormat"/> argument.
/// </summary>
/// <remarks>
/// For the JSON-array path the decoder currently uses <see cref="JsonDocument.ParseAsync(Stream, JsonDocumentOptions, CancellationToken)"/>,
/// which buffers the entire document before enumerating elements. A future follow-up can swap
/// in a <see cref="System.IO.Pipelines.PipeReader"/>-backed <see cref="Utf8JsonReader"/> for true
/// incremental streaming. For Phase C the element-by-element yield at the public surface is
/// what consumers observe.
/// </remarks>
public sealed class JsonSensorBatchDecoder(SensorBatchFormat format) : ISensorBatchDecoder
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<SensorReading> DecodeAsync(Stream content, [EnumeratorCancellation] CancellationToken ct)
    {
        if (format == SensorBatchFormat.JsonNdjson)
        {
            using var reader = new StreamReader(content, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return Parse(line);
            }
            yield break;
        }

        // Default: JSON array of objects.
        using var doc = await JsonDocument.ParseAsync(content, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            yield return Parse(el.GetRawText());
        }
    }

    private static SensorReading Parse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var r = doc.RootElement;
        return new SensorReading(
            SensorId: r.GetProperty("sensorId").GetString() ?? "",
            TimestampUtc: r.GetProperty("timestampUtc").GetDateTime().ToUniversalTime(),
            Kind: r.GetProperty("kind").GetString() ?? "",
            Value: r.GetProperty("value").GetDouble(),
            Unit: r.GetProperty("unit").GetString() ?? "");
    }
}

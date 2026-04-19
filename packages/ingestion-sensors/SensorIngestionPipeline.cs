using System.Text.Json;
using MessagePack;
using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Sensors.Decoders;

namespace Sunfish.Ingestion.Sensors;

/// <summary>
/// Ingestion pipeline for sensor batches. Archives the raw batch to <see cref="IBlobStore"/>,
/// decodes it via a format-appropriate <see cref="ISensorBatchDecoder"/>, and mints an
/// <see cref="IngestedEntity"/> carrying one <c>sensor.reading</c> event per reading plus a
/// batch-level <c>windowStartUtc</c>/<c>windowEndUtc</c> summary.
/// </summary>
public sealed class SensorIngestionPipeline(IBlobStore blobs) : IIngestionPipeline<SensorBatch>
{
    /// <inheritdoc/>
    public async ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        SensorBatch input, IngestionContext context, CancellationToken ct = default)
    {
        // 1. Buffer the raw bytes into blob store (batch archival).
        using var ms = new MemoryStream();
        await input.Content.CopyToAsync(ms, ct);
        var batchBytes = ms.ToArray();
        var batchCid = await blobs.PutAsync(batchBytes, ct);

        // 2. Select decoder by format.
        ISensorBatchDecoder decoder = input.Format switch
        {
            SensorBatchFormat.Json => new JsonSensorBatchDecoder(SensorBatchFormat.Json),
            SensorBatchFormat.JsonNdjson => new JsonSensorBatchDecoder(SensorBatchFormat.JsonNdjson),
            SensorBatchFormat.MessagePack => new MessagePackSensorBatchDecoder(),
            _ => throw new NotSupportedException($"Unknown format {input.Format}"),
        };

        // 3. Decode against fresh stream view (batch was already consumed above).
        var events = new List<IngestedEvent>();
        DateTime windowStart = DateTime.MaxValue;
        DateTime windowEnd = DateTime.MinValue;

        try
        {
            await foreach (var r in decoder.DecodeAsync(new MemoryStream(batchBytes, writable: false), ct))
            {
                if (r.TimestampUtc < windowStart) windowStart = r.TimestampUtc;
                if (r.TimestampUtc > windowEnd) windowEnd = r.TimestampUtc;

                events.Add(new IngestedEvent("sensor.reading", new Dictionary<string, object?>
                {
                    ["sensorId"] = r.SensorId,
                    ["timestampUtc"] = r.TimestampUtc,
                    ["kind"] = r.Kind,
                    ["value"] = r.Value,
                    ["unit"] = r.Unit,
                }, r.TimestampUtc));
            }
        }
        catch (NotSupportedException ex)
        {
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.UnsupportedFormat, ex.Message);
        }
        catch (JsonException ex)
        {
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.UnsupportedFormat, $"Malformed JSON: {ex.Message}");
        }
        catch (MessagePackSerializationException ex)
        {
            return IngestionResult<IngestedEntity>.Fail(IngestOutcome.UnsupportedFormat, $"Malformed MessagePack: {ex.Message}");
        }

        var body = new Dictionary<string, object?>
        {
            ["producerId"] = input.ProducerId,
            ["batchBlobCid"] = batchCid.Value,
            ["readingCount"] = events.Count,
            ["windowStartUtc"] = events.Count == 0 ? null : (object)windowStart,
            ["windowEndUtc"] = events.Count == 0 ? null : (object)windowEnd,
        };

        return IngestionResult<IngestedEntity>.Success(new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: events,
            BlobCids: new[] { batchCid }));
    }
}

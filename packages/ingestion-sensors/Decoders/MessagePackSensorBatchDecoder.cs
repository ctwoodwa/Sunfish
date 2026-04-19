using System.Runtime.CompilerServices;
using MessagePack;

namespace Sunfish.Ingestion.Sensors.Decoders;

/// <summary>
/// MessagePack decoder for sensor batches. Expects the payload to be a MessagePack array of
/// reading objects, each with string-keyed fields matching the JSON schema:
/// <c>sensorId</c>, <c>timestampUtc</c>, <c>kind</c>, <c>value</c>, <c>unit</c>.
/// </summary>
/// <remarks>
/// Uses <see cref="MessagePackSerializer"/> with <see cref="MessagePackSerializerOptions.Standard"/>
/// (the default resolver). Corrupt or structurally invalid payloads surface as
/// <see cref="MessagePackSerializationException"/>, which the <see cref="SensorIngestionPipeline"/>
/// maps to <see cref="Sunfish.Ingestion.Core.IngestOutcome.UnsupportedFormat"/>.
/// </remarks>
public sealed class MessagePackSensorBatchDecoder : ISensorBatchDecoder
{
    private static readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    /// <inheritdoc/>
    public async IAsyncEnumerable<SensorReading> DecodeAsync(
        Stream content,
        [EnumeratorCancellation] CancellationToken ct)
    {
        MsgPackSensorReadingDto[] batch;
        try
        {
            batch = await MessagePackSerializer.DeserializeAsync<MsgPackSensorReadingDto[]>(
                content, _options, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MessagePackSerializationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unexpected low-level exceptions so the pipeline can map them cleanly.
            throw new MessagePackSerializationException("Failed to deserialize MessagePack sensor batch.", ex);
        }

        foreach (var dto in batch ?? [])
        {
            ct.ThrowIfCancellationRequested();
            yield return dto.ToReading();
        }
    }
}

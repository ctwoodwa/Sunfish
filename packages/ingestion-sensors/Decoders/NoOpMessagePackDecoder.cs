using System.Runtime.CompilerServices;

namespace Sunfish.Ingestion.Sensors.Decoders;

/// <summary>
/// Placeholder decoder for <see cref="SensorBatchFormat.MessagePack"/>. MessagePack support is a
/// Phase-C parking-lot item; enumerating this decoder always throws
/// <see cref="NotSupportedException"/>, which the pipeline maps to
/// <see cref="Sunfish.Ingestion.Core.IngestOutcome.UnsupportedFormat"/>.
/// </summary>
public sealed class NoOpMessagePackDecoder : ISensorBatchDecoder
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<SensorReading> DecodeAsync(Stream content, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        throw new NotSupportedException("MessagePack decoding is a Phase-C parking-lot item. Use SensorBatchFormat.Json or .JsonNdjson.");
#pragma warning disable CS0162 // Unreachable code detected
        yield break;
#pragma warning restore CS0162
    }
}

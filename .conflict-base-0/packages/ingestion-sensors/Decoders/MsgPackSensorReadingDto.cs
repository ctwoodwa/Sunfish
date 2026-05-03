using MessagePack;

namespace Sunfish.Ingestion.Sensors.Decoders;

/// <summary>
/// MessagePack DTO for a single sensor reading. Uses string keys (map layout) via
/// <c>keyAsPropertyName: true</c> so the wire-format field names match the JSON schema:
/// <c>sensorId</c>, <c>timestampUtc</c>, <c>kind</c>, <c>value</c>, <c>unit</c>.
/// This type is internal to the ingestion-sensors package; callers receive <see cref="SensorReading"/>.
/// </summary>
[MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
internal sealed class MsgPackSensorReadingDto
{
    public string SensorId { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public string Kind { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";

    /// <summary>Maps this DTO to the canonical <see cref="SensorReading"/> record.</summary>
    internal SensorReading ToReading() => new(
        SensorId: SensorId,
        TimestampUtc: DateTime.SpecifyKind(TimestampUtc, DateTimeKind.Utc),
        Kind: Kind,
        Value: Value,
        Unit: Unit);
}

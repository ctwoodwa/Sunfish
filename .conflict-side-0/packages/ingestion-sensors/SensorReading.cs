namespace Sunfish.Ingestion.Sensors;

/// <summary>
/// A single decoded sensor measurement, emitted per-item by an
/// <see cref="Decoders.ISensorBatchDecoder"/>.
/// </summary>
/// <param name="SensorId">Stable identifier of the sensor producing the reading.</param>
/// <param name="TimestampUtc">UTC instant at which the measurement was taken.</param>
/// <param name="Kind">Measurement kind (e.g. <c>temperature</c>, <c>humidity</c>).</param>
/// <param name="Value">Numeric value of the measurement.</param>
/// <param name="Unit">Unit identifier (e.g. <c>celsius</c>, <c>percent_rh</c>).</param>
public sealed record SensorReading(
    string SensorId,
    DateTime TimestampUtc,
    string Kind,
    double Value,
    string Unit);

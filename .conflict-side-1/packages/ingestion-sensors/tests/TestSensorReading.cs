namespace Sunfish.Ingestion.Sensors.Tests;

/// <summary>
/// Plain POCO used to build MessagePack test payloads via ContractlessStandardResolver.
/// Must be public so the dynamic formatter can reflect over it.
/// Field names must match the production <c>MsgPackSensorReadingDto</c> property names exactly.
/// </summary>
public sealed class TestSensorReading
{
    public string SensorId { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
    public string Kind { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}

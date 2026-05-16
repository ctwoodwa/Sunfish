using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>Strongly-typed identifier for a <see cref="MaintenanceSchedule"/>. UUIDv7.</summary>
[JsonConverter(typeof(MaintenanceScheduleIdJsonConverter))]
public readonly record struct MaintenanceScheduleId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static MaintenanceScheduleId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MaintenanceScheduleId id) => id.Value;
}

internal sealed class MaintenanceScheduleIdJsonConverter : JsonConverter<MaintenanceScheduleId>
{
    public override MaintenanceScheduleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("MaintenanceScheduleId must be a non-null string.");
        return new MaintenanceScheduleId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceScheduleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

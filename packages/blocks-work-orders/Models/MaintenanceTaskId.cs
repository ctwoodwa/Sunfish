using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>Strongly-typed identifier for a <see cref="MaintenanceTask"/>. UUIDv7.</summary>
[JsonConverter(typeof(MaintenanceTaskIdJsonConverter))]
public readonly record struct MaintenanceTaskId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static MaintenanceTaskId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MaintenanceTaskId id) => id.Value;
}

internal sealed class MaintenanceTaskIdJsonConverter : JsonConverter<MaintenanceTaskId>
{
    public override MaintenanceTaskId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("MaintenanceTaskId must be a non-null string.");
        return new MaintenanceTaskId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceTaskId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

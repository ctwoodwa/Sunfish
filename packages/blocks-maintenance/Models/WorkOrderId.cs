using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Opaque identifier for a <see cref="WorkOrder"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(WorkOrderIdJsonConverter))]
public readonly record struct WorkOrderId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator WorkOrderId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(WorkOrderId id) => id.Value;

    /// <summary>Creates a new <see cref="WorkOrderId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static WorkOrderId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class WorkOrderIdJsonConverter : JsonConverter<WorkOrderId>
{
    public override WorkOrderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkOrderId must be a non-null string.");
        return new WorkOrderId(str);
    }

    public override void Write(Utf8JsonWriter writer, WorkOrderId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

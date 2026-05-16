using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>Strongly-typed identifier for a <see cref="WorkOrderLine"/>. UUIDv7.</summary>
[JsonConverter(typeof(WorkOrderLineIdJsonConverter))]
public readonly record struct WorkOrderLineId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static WorkOrderLineId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(WorkOrderLineId id) => id.Value;
}

internal sealed class WorkOrderLineIdJsonConverter : JsonConverter<WorkOrderLineId>
{
    public override WorkOrderLineId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkOrderLineId must be a non-null string.");
        return new WorkOrderLineId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, WorkOrderLineId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

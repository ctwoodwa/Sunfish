using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Strongly-typed identifier for a <see cref="WorkOrder"/>. Backed by
/// a UUIDv7 (sortable by mint-time) per
/// <c>_shared/engineering/crdt-friendly-schema-conventions.md</c> §1.
/// </summary>
// Inspired by Apache OFBiz WorkEffort module (Apache 2.0) — clean-room expression.
[JsonConverter(typeof(WorkOrderIdJsonConverter))]
public readonly record struct WorkOrderId(Guid Value)
{
    public override string ToString() => Value.ToString();

    /// <summary>Mint a fresh time-sortable id (UUIDv7).</summary>
    public static WorkOrderId NewId() => new(Guid.CreateVersion7());

    public static implicit operator Guid(WorkOrderId id) => id.Value;
}

internal sealed class WorkOrderIdJsonConverter : JsonConverter<WorkOrderId>
{
    public override WorkOrderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkOrderId must be a non-null string.");
        return new WorkOrderId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, WorkOrderId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

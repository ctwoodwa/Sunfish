using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>Strongly-typed identifier for a <see cref="RepairTicket"/>. UUIDv7.</summary>
[JsonConverter(typeof(RepairTicketIdJsonConverter))]
public readonly record struct RepairTicketId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static RepairTicketId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(RepairTicketId id) => id.Value;
}

internal sealed class RepairTicketIdJsonConverter : JsonConverter<RepairTicketId>
{
    public override RepairTicketId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RepairTicketId must be a non-null string.");
        return new RepairTicketId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, RepairTicketId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

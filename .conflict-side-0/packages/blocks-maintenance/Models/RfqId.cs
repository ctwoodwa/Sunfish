using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Opaque identifier for a <see cref="Rfq"/> (Request for Quote) record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(RfqIdJsonConverter))]
public readonly record struct RfqId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator RfqId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(RfqId id) => id.Value;

    /// <summary>Creates a new <see cref="RfqId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static RfqId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class RfqIdJsonConverter : JsonConverter<RfqId>
{
    public override RfqId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RfqId must be a non-null string.");
        return new RfqId(str);
    }

    public override void Write(Utf8JsonWriter writer, RfqId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

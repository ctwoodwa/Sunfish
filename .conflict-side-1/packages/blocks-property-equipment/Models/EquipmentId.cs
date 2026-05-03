using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>Opaque identifier for an <see cref="Equipment"/>.</summary>
[JsonConverter(typeof(EquipmentIdJsonConverter))]
public readonly record struct EquipmentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator EquipmentId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(EquipmentId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static EquipmentId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class EquipmentIdJsonConverter : JsonConverter<EquipmentId>
{
    public override EquipmentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("EquipmentId must be a non-null string.");
        return new EquipmentId(str);
    }

    public override void Write(Utf8JsonWriter writer, EquipmentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

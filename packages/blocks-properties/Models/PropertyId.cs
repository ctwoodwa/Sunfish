using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Properties.Models;

/// <summary>Opaque identifier for a <see cref="Property"/>.</summary>
[JsonConverter(typeof(PropertyIdJsonConverter))]
public readonly record struct PropertyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PropertyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PropertyId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static PropertyId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PropertyIdJsonConverter : JsonConverter<PropertyId>
{
    public override PropertyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PropertyId must be a non-null string.");
        return new PropertyId(str);
    }

    public override void Write(Utf8JsonWriter writer, PropertyId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

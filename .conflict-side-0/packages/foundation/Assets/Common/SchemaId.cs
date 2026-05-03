using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Opaque schema identifier. Phase A treats it as a string; the schema registry
/// (spec §3.4) will back it with a real type registry in a later phase.
/// </summary>
[JsonConverter(typeof(SchemaIdJsonConverter))]
public readonly record struct SchemaId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string for ergonomic literal usage.</summary>
    public static implicit operator SchemaId(string value) => new(value);

    /// <summary>Implicit conversion to string for log/DB serialization.</summary>
    public static implicit operator string(SchemaId id) => id.Value;
}

internal sealed class SchemaIdJsonConverter : JsonConverter<SchemaId>
{
    public override SchemaId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("SchemaId must be a non-null string.");
        return new SchemaId(str);
    }

    public override void Write(Utf8JsonWriter writer, SchemaId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for an <see cref="Inspection"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(InspectionIdJsonConverter))]
public readonly record struct InspectionId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InspectionId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InspectionId id) => id.Value;

    /// <summary>Creates a new <see cref="InspectionId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static InspectionId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InspectionIdJsonConverter : JsonConverter<InspectionId>
{
    public override InspectionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InspectionId must be a non-null string.");
        return new InspectionId(str);
    }

    public override void Write(Utf8JsonWriter writer, InspectionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

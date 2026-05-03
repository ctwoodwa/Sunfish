using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for an <see cref="InspectionTemplate"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(InspectionTemplateIdJsonConverter))]
public readonly record struct InspectionTemplateId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InspectionTemplateId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InspectionTemplateId id) => id.Value;

    /// <summary>Creates a new <see cref="InspectionTemplateId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static InspectionTemplateId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InspectionTemplateIdJsonConverter : JsonConverter<InspectionTemplateId>
{
    public override InspectionTemplateId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InspectionTemplateId must be a non-null string.");
        return new InspectionTemplateId(str);
    }

    public override void Write(Utf8JsonWriter writer, InspectionTemplateId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

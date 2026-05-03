using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for an <see cref="InspectionChecklistItem"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(InspectionChecklistItemIdJsonConverter))]
public readonly record struct InspectionChecklistItemId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InspectionChecklistItemId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InspectionChecklistItemId id) => id.Value;

    /// <summary>Creates a new <see cref="InspectionChecklistItemId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static InspectionChecklistItemId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InspectionChecklistItemIdJsonConverter : JsonConverter<InspectionChecklistItemId>
{
    public override InspectionChecklistItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InspectionChecklistItemId must be a non-null string.");
        return new InspectionChecklistItemId(str);
    }

    public override void Write(Utf8JsonWriter writer, InspectionChecklistItemId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

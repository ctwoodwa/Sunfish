using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for an <see cref="EquipmentConditionAssessment"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(EquipmentConditionAssessmentIdJsonConverter))]
public readonly record struct EquipmentConditionAssessmentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator EquipmentConditionAssessmentId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(EquipmentConditionAssessmentId id) => id.Value;

    /// <summary>Creates a new <see cref="EquipmentConditionAssessmentId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static EquipmentConditionAssessmentId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class EquipmentConditionAssessmentIdJsonConverter : JsonConverter<EquipmentConditionAssessmentId>
{
    public override EquipmentConditionAssessmentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("EquipmentConditionAssessmentId must be a non-null string.");
        return new EquipmentConditionAssessmentId(str);
    }

    public override void Write(Utf8JsonWriter writer, EquipmentConditionAssessmentId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for an <see cref="InspectionReport"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(InspectionReportIdJsonConverter))]
public readonly record struct InspectionReportId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InspectionReportId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InspectionReportId id) => id.Value;

    /// <summary>Creates a new <see cref="InspectionReportId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static InspectionReportId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InspectionReportIdJsonConverter : JsonConverter<InspectionReportId>
{
    public override InspectionReportId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InspectionReportId must be a non-null string.");
        return new InspectionReportId(str);
    }

    public override void Write(Utf8JsonWriter writer, InspectionReportId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

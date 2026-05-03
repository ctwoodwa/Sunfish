using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.BusinessCases.Models;

/// <summary>
/// Opaque identifier for a business-case record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(BusinessCaseIdJsonConverter))]
public readonly record struct BusinessCaseId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator BusinessCaseId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(BusinessCaseId id) => id.Value;

    /// <summary>Creates a new <see cref="BusinessCaseId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static BusinessCaseId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BusinessCaseIdJsonConverter : JsonConverter<BusinessCaseId>
{
    public override BusinessCaseId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BusinessCaseId must be a non-null string.");
        return new BusinessCaseId(str);
    }

    public override void Write(Utf8JsonWriter writer, BusinessCaseId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

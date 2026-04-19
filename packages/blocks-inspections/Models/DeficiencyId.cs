using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Opaque identifier for a <see cref="Deficiency"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(DeficiencyIdJsonConverter))]
public readonly record struct DeficiencyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator DeficiencyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(DeficiencyId id) => id.Value;

    /// <summary>Creates a new <see cref="DeficiencyId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static DeficiencyId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class DeficiencyIdJsonConverter : JsonConverter<DeficiencyId>
{
    public override DeficiencyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("DeficiencyId must be a non-null string.");
        return new DeficiencyId(str);
    }

    public override void Write(Utf8JsonWriter writer, DeficiencyId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>Opaque identifier for a <see cref="DepreciationSchedule"/>.</summary>
[JsonConverter(typeof(DepreciationScheduleIdJsonConverter))]
public readonly record struct DepreciationScheduleId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator DepreciationScheduleId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(DepreciationScheduleId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static DepreciationScheduleId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class DepreciationScheduleIdJsonConverter : JsonConverter<DepreciationScheduleId>
{
    public override DepreciationScheduleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("DepreciationScheduleId must be a non-null string.");
        return new DepreciationScheduleId(str);
    }

    public override void Write(Utf8JsonWriter writer, DepreciationScheduleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Opaque identifier for a <see cref="RentSchedule"/>.</summary>
[JsonConverter(typeof(RentScheduleIdJsonConverter))]
public readonly record struct RentScheduleId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator RentScheduleId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(RentScheduleId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static RentScheduleId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class RentScheduleIdJsonConverter : JsonConverter<RentScheduleId>
{
    public override RentScheduleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RentScheduleId must be a non-null string.");
        return new RentScheduleId(str);
    }

    public override void Write(Utf8JsonWriter writer, RentScheduleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

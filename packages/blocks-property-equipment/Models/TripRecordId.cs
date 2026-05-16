using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>Opaque identifier for a <see cref="TripRecord"/>.</summary>
[JsonConverter(typeof(TripRecordIdJsonConverter))]
public readonly record struct TripRecordId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TripRecordId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TripRecordId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TripRecordId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TripRecordIdJsonConverter : JsonConverter<TripRecordId>
{
    public override TripRecordId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? throw new JsonException("TripRecordId must be a non-null string."));

    public override void Write(Utf8JsonWriter writer, TripRecordId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

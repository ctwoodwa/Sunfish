using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>Opaque identifier for a <see cref="PartyAddress"/> row.</summary>
[JsonConverter(typeof(PartyAddressIdJsonConverter))]
public readonly record struct PartyAddressId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PartyAddressId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PartyAddressId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
    public static PartyAddressId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PartyAddressIdJsonConverter : JsonConverter<PartyAddressId>
{
    public override PartyAddressId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PartyAddressId must be a non-null string.");
        return new PartyAddressId(str);
    }

    public override void Write(Utf8JsonWriter writer, PartyAddressId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

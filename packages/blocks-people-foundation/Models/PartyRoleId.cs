using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>Opaque identifier for a <see cref="PartyRole"/> edge row.</summary>
[JsonConverter(typeof(PartyRoleIdJsonConverter))]
public readonly record struct PartyRoleId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PartyRoleId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PartyRoleId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
    public static PartyRoleId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PartyRoleIdJsonConverter : JsonConverter<PartyRoleId>
{
    public override PartyRoleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PartyRoleId must be a non-null string.");
        return new PartyRoleId(str);
    }

    public override void Write(Utf8JsonWriter writer, PartyRoleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

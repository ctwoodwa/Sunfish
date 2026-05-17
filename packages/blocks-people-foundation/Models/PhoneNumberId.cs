using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>Opaque identifier for a <see cref="PhoneNumber"/> row.</summary>
[JsonConverter(typeof(PhoneNumberIdJsonConverter))]
public readonly record struct PhoneNumberId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PhoneNumberId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PhoneNumberId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
    public static PhoneNumberId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PhoneNumberIdJsonConverter : JsonConverter<PhoneNumberId>
{
    public override PhoneNumberId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PhoneNumberId must be a non-null string.");
        return new PhoneNumberId(str);
    }

    public override void Write(Utf8JsonWriter writer, PhoneNumberId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

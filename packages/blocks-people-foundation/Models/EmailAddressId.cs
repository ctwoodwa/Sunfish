using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>Opaque identifier for an <see cref="EmailAddress"/> row.</summary>
[JsonConverter(typeof(EmailAddressIdJsonConverter))]
public readonly record struct EmailAddressId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator EmailAddressId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(EmailAddressId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
    public static EmailAddressId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class EmailAddressIdJsonConverter : JsonConverter<EmailAddressId>
{
    public override EmailAddressId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("EmailAddressId must be a non-null string.");
        return new EmailAddressId(str);
    }

    public override void Write(Utf8JsonWriter writer, EmailAddressId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

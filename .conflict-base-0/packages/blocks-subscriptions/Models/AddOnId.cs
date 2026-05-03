using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// Opaque identifier for an <see cref="AddOn"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(AddOnIdJsonConverter))]
public readonly record struct AddOnId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator AddOnId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(AddOnId id) => id.Value;

    /// <summary>Creates a new <see cref="AddOnId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static AddOnId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class AddOnIdJsonConverter : JsonConverter<AddOnId>
{
    public override AddOnId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("AddOnId must be a non-null string.");
        return new AddOnId(str);
    }

    public override void Write(Utf8JsonWriter writer, AddOnId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

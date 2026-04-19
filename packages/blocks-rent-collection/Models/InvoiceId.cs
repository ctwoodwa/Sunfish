using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Opaque identifier for an <see cref="Invoice"/>.</summary>
[JsonConverter(typeof(InvoiceIdJsonConverter))]
public readonly record struct InvoiceId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InvoiceId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InvoiceId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static InvoiceId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InvoiceIdJsonConverter : JsonConverter<InvoiceId>
{
    public override InvoiceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InvoiceId must be a non-null string.");
        return new InvoiceId(str);
    }

    public override void Write(Utf8JsonWriter writer, InvoiceId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

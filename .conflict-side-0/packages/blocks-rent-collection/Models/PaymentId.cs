using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Opaque identifier for a <see cref="Payment"/>.</summary>
[JsonConverter(typeof(PaymentIdJsonConverter))]
public readonly record struct PaymentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PaymentId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PaymentId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static PaymentId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PaymentIdJsonConverter : JsonConverter<PaymentId>
{
    public override PaymentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PaymentId must be a non-null string.");
        return new PaymentId(str);
    }

    public override void Write(Utf8JsonWriter writer, PaymentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

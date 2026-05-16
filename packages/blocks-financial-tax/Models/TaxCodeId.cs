using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>Opaque identifier for a <see cref="TaxCode"/>.</summary>
[JsonConverter(typeof(TaxCodeIdJsonConverter))]
public readonly record struct TaxCodeId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TaxCodeId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TaxCodeId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TaxCodeId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TaxCodeIdJsonConverter : JsonConverter<TaxCodeId>
{
    public override TaxCodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TaxCodeId must be a non-null string.");
        return new TaxCodeId(str);
    }

    public override void Write(Utf8JsonWriter writer, TaxCodeId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

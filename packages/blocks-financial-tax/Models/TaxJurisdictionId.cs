using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>Opaque identifier for a <see cref="TaxJurisdiction"/>.</summary>
[JsonConverter(typeof(TaxJurisdictionIdJsonConverter))]
public readonly record struct TaxJurisdictionId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TaxJurisdictionId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TaxJurisdictionId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TaxJurisdictionId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TaxJurisdictionIdJsonConverter : JsonConverter<TaxJurisdictionId>
{
    public override TaxJurisdictionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TaxJurisdictionId must be a non-null string.");
        return new TaxJurisdictionId(str);
    }

    public override void Write(Utf8JsonWriter writer, TaxJurisdictionId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Opaque identifier for a <c>TaxRate</c>. The entity itself ships in
/// PR 2 of the blocks-financial-tax-stage06-handoff; the id type lands
/// in PR 1 so downstream consumers (TaxCode change-history queries
/// declared in PR 2) compile against a stable type.
/// </summary>
[JsonConverter(typeof(TaxRateIdJsonConverter))]
public readonly record struct TaxRateId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TaxRateId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TaxRateId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TaxRateId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TaxRateIdJsonConverter : JsonConverter<TaxRateId>
{
    public override TaxRateId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TaxRateId must be a non-null string.");
        return new TaxRateId(str);
    }

    public override void Write(Utf8JsonWriter writer, TaxRateId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

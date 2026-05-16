using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Opaque identifier for a <c>TaxFormLineMap</c>. The entity ships in
/// PR 4 of the blocks-financial-tax-stage06-handoff; the id type lands
/// in PR 1 so downstream consumers (Schedule E generator in
/// <c>Sunfish.Blocks.Reports.Tax</c>) can compile against a stable
/// type without taking a project-reference to PR 4 code.
/// </summary>
[JsonConverter(typeof(TaxFormLineMapIdJsonConverter))]
public readonly record struct TaxFormLineMapId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TaxFormLineMapId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TaxFormLineMapId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TaxFormLineMapId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TaxFormLineMapIdJsonConverter : JsonConverter<TaxFormLineMapId>
{
    public override TaxFormLineMapId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TaxFormLineMapId must be a non-null string.");
        return new TaxFormLineMapId(str);
    }

    public override void Write(Utf8JsonWriter writer, TaxFormLineMapId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>Opaque identifier for an <see cref="InvoiceLine"/>.</summary>
[JsonConverter(typeof(InvoiceLineIdJsonConverter))]
public readonly record struct InvoiceLineId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator InvoiceLineId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(InvoiceLineId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
    public static InvoiceLineId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class InvoiceLineIdJsonConverter : JsonConverter<InvoiceLineId>
{
    public override InvoiceLineId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("InvoiceLineId must be a non-null string.");
        return new InvoiceLineId(str);
    }

    public override void Write(Utf8JsonWriter writer, InvoiceLineId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

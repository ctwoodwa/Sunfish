using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>Opaque identifier for a <see cref="BillLine"/>.</summary>
[JsonConverter(typeof(BillLineIdJsonConverter))]
public readonly record struct BillLineId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
    public static implicit operator BillLineId(string value) => new(value);
    public static implicit operator string(BillLineId id) => id.Value;
    public static BillLineId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BillLineIdJsonConverter : JsonConverter<BillLineId>
{
    public override BillLineId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BillLineId must be a non-null string.");
        return new BillLineId(str);
    }

    public override void Write(Utf8JsonWriter writer, BillLineId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

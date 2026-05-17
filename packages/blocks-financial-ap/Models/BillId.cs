using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>Opaque identifier for a <see cref="Bill"/>.</summary>
[JsonConverter(typeof(BillIdJsonConverter))]
public readonly record struct BillId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
    public static implicit operator BillId(string value) => new(value);
    public static implicit operator string(BillId id) => id.Value;
    public static BillId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BillIdJsonConverter : JsonConverter<BillId>
{
    public override BillId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BillId must be a non-null string.");
        return new BillId(str);
    }

    public override void Write(Utf8JsonWriter writer, BillId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

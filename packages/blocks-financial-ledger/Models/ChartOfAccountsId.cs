using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>Opaque identifier for a <see cref="ChartOfAccounts"/>.</summary>
[JsonConverter(typeof(ChartOfAccountsIdJsonConverter))]
public readonly record struct ChartOfAccountsId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ChartOfAccountsId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ChartOfAccountsId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static ChartOfAccountsId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ChartOfAccountsIdJsonConverter : JsonConverter<ChartOfAccountsId>
{
    public override ChartOfAccountsId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ChartOfAccountsId must be a non-null string.");
        return new ChartOfAccountsId(str);
    }

    public override void Write(Utf8JsonWriter writer, ChartOfAccountsId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

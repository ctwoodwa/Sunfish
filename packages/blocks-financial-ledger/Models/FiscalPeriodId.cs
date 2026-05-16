using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Opaque identifier for a fiscal period (the entity itself lives in
/// <c>blocks-financial-periods</c>; this ID type is hosted here so
/// <see cref="JournalEntry.PeriodId"/> can FK-reference it without a
/// reverse cluster dependency on <c>blocks-financial-periods</c>).
/// Nullable on <see cref="JournalEntry"/> in PR 3; will become mandatory
/// once the periods cluster ships.
/// </summary>
[JsonConverter(typeof(FiscalPeriodIdJsonConverter))]
public readonly record struct FiscalPeriodId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator FiscalPeriodId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(FiscalPeriodId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static FiscalPeriodId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class FiscalPeriodIdJsonConverter : JsonConverter<FiscalPeriodId>
{
    public override FiscalPeriodId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("FiscalPeriodId must be a non-null string.");
        return new FiscalPeriodId(str);
    }

    public override void Write(Utf8JsonWriter writer, FiscalPeriodId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

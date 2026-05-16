using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>Opaque identifier for a <see cref="FiscalYear"/>.</summary>
[JsonConverter(typeof(FiscalYearIdJsonConverter))]
public readonly record struct FiscalYearId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator FiscalYearId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(FiscalYearId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static FiscalYearId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class FiscalYearIdJsonConverter : JsonConverter<FiscalYearId>
{
    public override FiscalYearId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("FiscalYearId must be a non-null string.");
        return new FiscalYearId(str);
    }

    public override void Write(Utf8JsonWriter writer, FiscalYearId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

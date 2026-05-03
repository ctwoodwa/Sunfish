using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>Opaque identifier for a <see cref="TaxReport"/>. Wire form: plain string (UUID recommended).</summary>
[JsonConverter(typeof(TaxReportIdJsonConverter))]
public readonly record struct TaxReportId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TaxReportId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TaxReportId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static TaxReportId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TaxReportIdJsonConverter : JsonConverter<TaxReportId>
{
    public override TaxReportId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TaxReportId must be a non-null string.");
        return new TaxReportId(str);
    }

    public override void Write(Utf8JsonWriter writer, TaxReportId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

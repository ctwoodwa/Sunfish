using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Opaque identifier for a <see cref="Quote"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(QuoteIdJsonConverter))]
public readonly record struct QuoteId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator QuoteId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(QuoteId id) => id.Value;

    /// <summary>Creates a new <see cref="QuoteId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static QuoteId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class QuoteIdJsonConverter : JsonConverter<QuoteId>
{
    public override QuoteId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("QuoteId must be a non-null string.");
        return new QuoteId(str);
    }

    public override void Write(Utf8JsonWriter writer, QuoteId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

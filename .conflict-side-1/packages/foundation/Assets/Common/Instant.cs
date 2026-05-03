using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Thin wrapper over <see cref="DateTimeOffset"/> that aligns the Sunfish asset-modeling
/// code with spec §3 vocabulary. The runtime type is <see cref="DateTimeOffset"/>;
/// this wrapper preserves timezone information and round-trips cleanly to Postgres
/// <c>timestamptz</c>.
/// </summary>
[JsonConverter(typeof(InstantJsonConverter))]
public readonly record struct Instant(DateTimeOffset Value)
{
    /// <summary>The current UTC instant.</summary>
    public static Instant Now => new(DateTimeOffset.UtcNow);

    /// <summary>The minimum representable instant.</summary>
    public static Instant MinValue => new(DateTimeOffset.MinValue);

    /// <summary>The maximum representable instant.</summary>
    public static Instant MaxValue => new(DateTimeOffset.MaxValue);

    /// <summary>ISO-8601 string form, round-trip safe (<c>"O"</c> format).</summary>
    public override string ToString() => Value.ToString("O");

    /// <summary>Implicit conversion from <see cref="DateTimeOffset"/>.</summary>
    public static implicit operator Instant(DateTimeOffset value) => new(value);

    /// <summary>Implicit conversion to <see cref="DateTimeOffset"/>.</summary>
    public static implicit operator DateTimeOffset(Instant instant) => instant.Value;
}

internal sealed class InstantJsonConverter : JsonConverter<Instant>
{
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("Instant must be a non-null ISO-8601 string.");
        if (!DateTimeOffset.TryParseExact(str, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
            throw new JsonException($"Instant '{str}' is not a valid ISO-8601 round-trip string.");
        return new Instant(dto);
    }

    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString("O"));
    }
}

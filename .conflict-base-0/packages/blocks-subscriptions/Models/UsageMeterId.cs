using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// Opaque identifier for a <see cref="UsageMeter"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(UsageMeterIdJsonConverter))]
public readonly record struct UsageMeterId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator UsageMeterId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(UsageMeterId id) => id.Value;

    /// <summary>Creates a new <see cref="UsageMeterId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static UsageMeterId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class UsageMeterIdJsonConverter : JsonConverter<UsageMeterId>
{
    public override UsageMeterId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("UsageMeterId must be a non-null string.");
        return new UsageMeterId(str);
    }

    public override void Write(Utf8JsonWriter writer, UsageMeterId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

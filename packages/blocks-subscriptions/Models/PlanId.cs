using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// Opaque identifier for a <see cref="Plan"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(PlanIdJsonConverter))]
public readonly record struct PlanId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PlanId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PlanId id) => id.Value;

    /// <summary>Creates a new <see cref="PlanId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static PlanId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PlanIdJsonConverter : JsonConverter<PlanId>
{
    public override PlanId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PlanId must be a non-null string.");
        return new PlanId(str);
    }

    public override void Write(Utf8JsonWriter writer, PlanId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

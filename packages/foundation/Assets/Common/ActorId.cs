using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Opaque identifier for an acting principal (user / service / system).
/// </summary>
/// <remarks>
/// Two well-known sentinels:
/// <list type="bullet">
/// <item><description><see cref="System"/> — the system-internal actor used when no ambient context is available (e.g., scheduled jobs, background workers).</description></item>
/// <item><description><see cref="Sunfish"/> — the Sunfish-shipped Authoritative actor, used as the owner of compliance-source taxonomy definitions and other Authoritative-regime artifacts (per ADR 0056).</description></item>
/// </list>
/// </remarks>
[JsonConverter(typeof(ActorIdJsonConverter))]
public readonly record struct ActorId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ActorId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ActorId id) => id.Value;

    /// <summary>The system-internal actor used when no ambient context is available.</summary>
    public static ActorId System { get; } = new("system");

    /// <summary>The Sunfish-shipped Authoritative actor — owner of compliance-source taxonomy definitions and other Authoritative-regime artifacts (per ADR 0056).</summary>
    public static ActorId Sunfish { get; } = new("sunfish");
}

internal sealed class ActorIdJsonConverter : JsonConverter<ActorId>
{
    public override ActorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ActorId must be a non-null string.");
        return new ActorId(str);
    }

    public override void Write(Utf8JsonWriter writer, ActorId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

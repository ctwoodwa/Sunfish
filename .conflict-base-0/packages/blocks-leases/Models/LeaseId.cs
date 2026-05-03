using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Opaque identifier for a <see cref="Lease"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(LeaseIdJsonConverter))]
public readonly record struct LeaseId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator LeaseId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(LeaseId id) => id.Value;

    /// <summary>Creates a new <see cref="LeaseId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static LeaseId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class LeaseIdJsonConverter : JsonConverter<LeaseId>
{
    public override LeaseId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("LeaseId must be a non-null string.");
        return new LeaseId(str);
    }

    public override void Write(Utf8JsonWriter writer, LeaseId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

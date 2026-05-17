using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Opaque identifier for a lease-local <see cref="Party"/> record.
/// </summary>
/// <remarks>
/// <b>DEPRECATED — use <see cref="Sunfish.Blocks.People.Foundation.Models.PartyId"/> instead.</b>
/// Predates the canonical party-model convention (see
/// <c>_shared/engineering/party-model-convention.md</c>). The wire
/// format is identical (string-backed), so values round-trip across
/// the two types via the implicit string converters; new code should
/// consume the canonical type directly. Removal is a future
/// <c>sunfish-api-change</c> pipeline step (NOT in scope here).
/// </remarks>
#pragma warning disable CS0618 // type-internal references to the obsolete PartyId
[Obsolete("Use Sunfish.Blocks.People.Foundation.Models.PartyId instead. Wire format is compatible; convert via implicit string operators. Removal is a future sunfish-api-change pipeline step.")]
[JsonConverter(typeof(PartyIdJsonConverter))]
public readonly record struct PartyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PartyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PartyId id) => id.Value;

    /// <summary>Creates a new <see cref="PartyId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static PartyId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PartyIdJsonConverter : JsonConverter<PartyId>
{
    public override PartyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PartyId must be a non-null string.");
        return new PartyId(str);
    }

    public override void Write(Utf8JsonWriter writer, PartyId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
#pragma warning restore CS0618

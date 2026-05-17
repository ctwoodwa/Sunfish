using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>Opaque identifier for a <see cref="Party"/>.</summary>
/// <remarks>
/// Backed by <see cref="Guid"/> per current Sunfish ID convention (see
/// e.g. <c>TaxCodeId</c>, <c>GLAccountId</c>). The people-foundation hand-off
/// proposed ULID-backing for time-ordered ID locality; that change is deferred
/// pending a repo-wide sweep so all aggregate roots flip together.
/// </remarks>
[JsonConverter(typeof(PartyIdJsonConverter))]
public readonly record struct PartyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PartyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PartyId id) => id.Value;

    /// <summary>Generates a new unique id.</summary>
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
        => writer.WriteStringValue(value.Value);
}

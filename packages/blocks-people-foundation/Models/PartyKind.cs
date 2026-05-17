using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// The fundamental dichotomy of a <see cref="Party"/> — a real human, or an
/// organization (LLC, sole-prop, trust, etc.). Per party-model-convention §3:
/// a party MUST be exactly one Kind for its lifetime; transitions are not
/// modeled (a single-member LLC owner does NOT become an Organization when
/// the LLC is formed — those are two distinct parties tied by a role-edge).
/// </summary>
[JsonConverter(typeof(PartyKindJsonConverter))]
public enum PartyKind
{
    /// <summary>A real human.</summary>
    Person,

    /// <summary>An organization — LLC, corp, sole-prop, trust, partnership, govt agency, etc.</summary>
    Organization,
}

/// <summary>
/// Persists <see cref="PartyKind"/> as lowercase string codes (<c>"person"</c>
/// / <c>"organization"</c>) rather than numeric values, so on-disk payloads
/// survive enum reordering and read clearly in JSON dumps.
/// </summary>
internal sealed class PartyKindJsonConverter : JsonConverter<PartyKind>
{
    public override PartyKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return str switch
        {
            "person"       => PartyKind.Person,
            "organization" => PartyKind.Organization,
            _ => throw new JsonException($"Unknown PartyKind '{str}'. Expected 'person' or 'organization'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, PartyKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PartyKind.Person       => "person",
            PartyKind.Organization => "organization",
            _ => throw new JsonException($"Unknown PartyKind '{value}'."),
        });
    }
}

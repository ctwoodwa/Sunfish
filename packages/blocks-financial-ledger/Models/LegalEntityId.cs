using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Opaque identifier for a legal entity (LLC / corporation / sole-prop)
/// that owns a chart of accounts.
/// </summary>
/// <remarks>
/// W#60 P4 hand-off note: this type should ultimately live in
/// <c>Sunfish.Foundation.Identity</c> alongside other foundation-level
/// identity types. Stubbed locally here because that package does not
/// exist on main yet; relocate when it lands.
/// TODO: relocate to <c>foundation-identity</c> when that package lands.
/// </remarks>
[JsonConverter(typeof(LegalEntityIdJsonConverter))]
public readonly record struct LegalEntityId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator LegalEntityId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(LegalEntityId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static LegalEntityId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class LegalEntityIdJsonConverter : JsonConverter<LegalEntityId>
{
    public override LegalEntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("LegalEntityId must be a non-null string.");
        return new LegalEntityId(str);
    }

    public override void Write(Utf8JsonWriter writer, LegalEntityId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

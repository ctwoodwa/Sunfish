using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Opaque identifier for a property (cost-center dimensional tag on
/// <see cref="JournalEntryLine.PropertyId"/>).
/// </summary>
/// <remarks>
/// W#60 P4 hand-off note: the canonical <c>PropertyId</c> lives in
/// <c>Sunfish.Blocks.Properties.Models</c>. Stubbed locally here so
/// blocks-financial-ledger does not take a reverse cluster dependency
/// on blocks-properties (financial is more foundational than property
/// per the cluster topology). Relocate when a shared cross-cluster FK
/// type lands in foundation-identity (same migration path as
/// <see cref="LegalEntityId"/>).
/// TODO: relocate to shared cross-cluster FK package when it lands.
/// </remarks>
[JsonConverter(typeof(PropertyIdJsonConverter))]
public readonly record struct PropertyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator PropertyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(PropertyId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static PropertyId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class PropertyIdJsonConverter : JsonConverter<PropertyId>
{
    public override PropertyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PropertyId must be a non-null string.");
        return new PropertyId(str);
    }

    public override void Write(Utf8JsonWriter writer, PropertyId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Strongly-typed identifier for a <see cref="Contractor"/>. Distinct
/// from the underlying <c>PartyId</c> because the contractor
/// projection carries vendor-side fields (insurance, license,
/// trades) that don't belong on <c>Party</c>. UUIDv7.
/// </summary>
[JsonConverter(typeof(ContractorIdJsonConverter))]
public readonly record struct ContractorId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static ContractorId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ContractorId id) => id.Value;
}

internal sealed class ContractorIdJsonConverter : JsonConverter<ContractorId>
{
    public override ContractorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ContractorId must be a non-null string.");
        return new ContractorId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, ContractorId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Opaque identifier for a <see cref="LateFeePolicy"/>.</summary>
[JsonConverter(typeof(LateFeePolicyIdJsonConverter))]
public readonly record struct LateFeePolicyId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator LateFeePolicyId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(LateFeePolicyId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static LateFeePolicyId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class LateFeePolicyIdJsonConverter : JsonConverter<LateFeePolicyId>
{
    public override LateFeePolicyId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("LateFeePolicyId must be a non-null string.");
        return new LateFeePolicyId(str);
    }

    public override void Write(Utf8JsonWriter writer, LateFeePolicyId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

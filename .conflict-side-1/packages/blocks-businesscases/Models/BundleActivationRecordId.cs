using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.BusinessCases.Models;

/// <summary>
/// Opaque identifier for a <see cref="BundleActivationRecord"/>.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(BundleActivationRecordIdJsonConverter))]
public readonly record struct BundleActivationRecordId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator BundleActivationRecordId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(BundleActivationRecordId id) => id.Value;

    /// <summary>Creates a new <see cref="BundleActivationRecordId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static BundleActivationRecordId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BundleActivationRecordIdJsonConverter : JsonConverter<BundleActivationRecordId>
{
    public override BundleActivationRecordId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BundleActivationRecordId must be a non-null string.");
        return new BundleActivationRecordId(str);
    }

    public override void Write(Utf8JsonWriter writer, BundleActivationRecordId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

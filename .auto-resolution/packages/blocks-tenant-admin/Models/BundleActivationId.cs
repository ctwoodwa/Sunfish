using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// Opaque identifier for a <see cref="BundleActivation"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(BundleActivationIdJsonConverter))]
public readonly record struct BundleActivationId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator BundleActivationId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(BundleActivationId id) => id.Value;

    /// <summary>Creates a new <see cref="BundleActivationId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static BundleActivationId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class BundleActivationIdJsonConverter : JsonConverter<BundleActivationId>
{
    public override BundleActivationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("BundleActivationId must be a non-null string.");
        return new BundleActivationId(str);
    }

    public override void Write(Utf8JsonWriter writer, BundleActivationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

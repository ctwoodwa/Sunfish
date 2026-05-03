using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Opaque identifier for a <see cref="Vendor"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(VendorIdJsonConverter))]
public readonly record struct VendorId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator VendorId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(VendorId id) => id.Value;

    /// <summary>Creates a new <see cref="VendorId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static VendorId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class VendorIdJsonConverter : JsonConverter<VendorId>
{
    public override VendorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("VendorId must be a non-null string.");
        return new VendorId(str);
    }

    public override void Write(Utf8JsonWriter writer, VendorId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

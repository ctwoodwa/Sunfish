using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// Opaque identifier for a <see cref="TenantUser"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(TenantUserIdJsonConverter))]
public readonly record struct TenantUserId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TenantUserId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TenantUserId id) => id.Value;

    /// <summary>Creates a new <see cref="TenantUserId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static TenantUserId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class TenantUserIdJsonConverter : JsonConverter<TenantUserId>
{
    public override TenantUserId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TenantUserId must be a non-null string.");
        return new TenantUserId(str);
    }

    public override void Write(Utf8JsonWriter writer, TenantUserId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>Opaque tenant identifier for multi-tenant data isolation.</summary>
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TenantId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TenantId id) => id.Value;

    /// <summary>The default tenant used when no explicit tenant is provided.</summary>
    public static TenantId Default { get; } = new("default");
}

internal sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TenantId must be a non-null string.");
        return new TenantId(str);
    }

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

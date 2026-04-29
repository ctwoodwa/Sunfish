using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.PropertyAssets.Models;

/// <summary>Opaque identifier for an <see cref="Asset"/>.</summary>
[JsonConverter(typeof(AssetIdJsonConverter))]
public readonly record struct AssetId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator AssetId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(AssetId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static AssetId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class AssetIdJsonConverter : JsonConverter<AssetId>
{
    public override AssetId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("AssetId must be a non-null string.");
        return new AssetId(str);
    }

    public override void Write(Utf8JsonWriter writer, AssetId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

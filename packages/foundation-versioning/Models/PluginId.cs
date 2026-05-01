using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Opaque identifier for a Sunfish plugin in a <see cref="VersionVector"/>'s
/// plugins map (ADR 0028-A6.2 rule 3 / A7.3 augmentation).
/// </summary>
[JsonConverter(typeof(PluginIdJsonConverter))]
public readonly record struct PluginId(string Value)
{
    public override string ToString() => Value;
}

internal sealed class PluginIdJsonConverter : JsonConverter<PluginId>
{
    public override PluginId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PluginId must be a non-null string.");
        return new PluginId(str);
    }

    public override void Write(Utf8JsonWriter writer, PluginId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }

    public override PluginId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("PluginId property name must be a non-null string.");
        return new PluginId(str);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, PluginId value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.Value);
    }
}

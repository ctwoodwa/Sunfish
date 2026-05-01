using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Opaque identifier for a Sunfish UI adapter (e.g., <c>"blazor"</c>,
/// <c>"react"</c>, <c>"maui-blazor"</c>) in a <see cref="VersionVector"/>'s
/// adapters map. Per ADR 0028-A6.2 rule 4, adapter-set asymmetry does NOT
/// block federation by itself — peers ride the intersection.
/// </summary>
[JsonConverter(typeof(AdapterIdJsonConverter))]
public readonly record struct AdapterId(string Value)
{
    public override string ToString() => Value;
}

internal sealed class AdapterIdJsonConverter : JsonConverter<AdapterId>
{
    public override AdapterId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("AdapterId must be a non-null string.");
        return new AdapterId(str);
    }

    public override void Write(Utf8JsonWriter writer, AdapterId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }

    public override AdapterId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("AdapterId property name must be a non-null string.");
        return new AdapterId(str);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, AdapterId value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.Value);
    }
}

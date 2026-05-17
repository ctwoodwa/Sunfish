using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>Opaque identifier for a <see cref="DocumentRef"/>.</summary>
[JsonConverter(typeof(DocumentRefIdJsonConverter))]
public readonly record struct DocumentRefId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
    public static implicit operator DocumentRefId(string value) => new(value);
    public static implicit operator string(DocumentRefId id) => id.Value;
    public static DocumentRefId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class DocumentRefIdJsonConverter : JsonConverter<DocumentRefId>
{
    public override DocumentRefId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("DocumentRefId must be a non-null string.");
        return new DocumentRefId(str);
    }

    public override void Write(Utf8JsonWriter writer, DocumentRefId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

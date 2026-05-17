using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>Opaque identifier for an <see cref="Attachment"/>.</summary>
[JsonConverter(typeof(AttachmentIdJsonConverter))]
public readonly record struct AttachmentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
    public static implicit operator AttachmentId(string value) => new(value);
    public static implicit operator string(AttachmentId id) => id.Value;
    public static AttachmentId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class AttachmentIdJsonConverter : JsonConverter<AttachmentId>
{
    public override AttachmentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("AttachmentId must be a non-null string.");
        return new AttachmentId(str);
    }

    public override void Write(Utf8JsonWriter writer, AttachmentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

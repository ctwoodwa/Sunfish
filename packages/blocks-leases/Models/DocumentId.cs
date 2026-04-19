using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Opaque identifier for a <see cref="Document"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(DocumentIdJsonConverter))]
public readonly record struct DocumentId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator DocumentId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(DocumentId id) => id.Value;

    /// <summary>Creates a new <see cref="DocumentId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static DocumentId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class DocumentIdJsonConverter : JsonConverter<DocumentId>
{
    public override DocumentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("DocumentId must be a non-null string.");
        return new DocumentId(str);
    }

    public override void Write(Utf8JsonWriter writer, DocumentId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

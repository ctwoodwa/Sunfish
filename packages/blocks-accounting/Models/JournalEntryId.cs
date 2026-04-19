using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>Opaque identifier for a <see cref="JournalEntry"/>.</summary>
[JsonConverter(typeof(JournalEntryIdJsonConverter))]
public readonly record struct JournalEntryId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator JournalEntryId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(JournalEntryId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static JournalEntryId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class JournalEntryIdJsonConverter : JsonConverter<JournalEntryId>
{
    public override JournalEntryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("JournalEntryId must be a non-null string.");
        return new JournalEntryId(str);
    }

    public override void Write(Utf8JsonWriter writer, JournalEntryId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

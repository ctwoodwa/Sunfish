using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="TimeEntry"/>. UUIDv7.</summary>
[JsonConverter(typeof(TimeEntryIdJsonConverter))]
public readonly record struct TimeEntryId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static TimeEntryId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(TimeEntryId id) => id.Value;
}

internal sealed class TimeEntryIdJsonConverter : JsonConverter<TimeEntryId>
{
    public override TimeEntryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TimeEntryId must be a non-null string.");
        return new TimeEntryId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, TimeEntryId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="ProjectMilestone"/>. UUIDv7.</summary>
[JsonConverter(typeof(MilestoneIdJsonConverter))]
public readonly record struct MilestoneId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static MilestoneId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MilestoneId id) => id.Value;
}

internal sealed class MilestoneIdJsonConverter : JsonConverter<MilestoneId>
{
    public override MilestoneId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("MilestoneId must be a non-null string.");
        return new MilestoneId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, MilestoneId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

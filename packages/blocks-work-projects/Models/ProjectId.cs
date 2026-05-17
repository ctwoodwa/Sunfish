using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="Project"/>. UUIDv7.</summary>
// Inspired by Apache OFBiz WorkEffort module (Apache 2.0) — clean-room expression.
[JsonConverter(typeof(ProjectIdJsonConverter))]
public readonly record struct ProjectId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static ProjectId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProjectId id) => id.Value;
}

internal sealed class ProjectIdJsonConverter : JsonConverter<ProjectId>
{
    public override ProjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ProjectId must be a non-null string.");
        return new ProjectId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, ProjectId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

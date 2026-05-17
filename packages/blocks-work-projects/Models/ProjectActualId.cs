using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="ProjectActual"/> row. UUIDv7.</summary>
[JsonConverter(typeof(ProjectActualIdJsonConverter))]
public readonly record struct ProjectActualId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static ProjectActualId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProjectActualId id) => id.Value;
}

internal sealed class ProjectActualIdJsonConverter : JsonConverter<ProjectActualId>
{
    public override ProjectActualId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ProjectActualId must be a non-null string.");
        return new ProjectActualId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, ProjectActualId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="ProjectBudgetLine"/>. UUIDv7.</summary>
[JsonConverter(typeof(ProjectBudgetLineIdJsonConverter))]
public readonly record struct ProjectBudgetLineId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static ProjectBudgetLineId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProjectBudgetLineId id) => id.Value;
}

internal sealed class ProjectBudgetLineIdJsonConverter : JsonConverter<ProjectBudgetLineId>
{
    public override ProjectBudgetLineId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ProjectBudgetLineId must be a non-null string.");
        return new ProjectBudgetLineId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, ProjectBudgetLineId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="ProjectBudget"/> revision. UUIDv7.</summary>
[JsonConverter(typeof(ProjectBudgetIdJsonConverter))]
public readonly record struct ProjectBudgetId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static ProjectBudgetId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProjectBudgetId id) => id.Value;
}

internal sealed class ProjectBudgetIdJsonConverter : JsonConverter<ProjectBudgetId>
{
    public override ProjectBudgetId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ProjectBudgetId must be a non-null string.");
        return new ProjectBudgetId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, ProjectBudgetId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Strongly-typed, Guid-backed identifier for a workflow instance.
/// Serializes to/from a flat JSON string (no wrapper object).
/// </summary>
[JsonConverter(typeof(WorkflowInstanceIdJsonConverter))]
public readonly record struct WorkflowInstanceId(Guid Value)
{
    /// <summary>Creates a new, unique <see cref="WorkflowInstanceId"/>.</summary>
    public static WorkflowInstanceId NewId() => new(Guid.NewGuid());

    /// <summary>Parses a <see cref="WorkflowInstanceId"/> from a GUID string.</summary>
    public static WorkflowInstanceId Parse(string value) => new(Guid.Parse(value));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

internal sealed class WorkflowInstanceIdJsonConverter : JsonConverter<WorkflowInstanceId>
{
    public override WorkflowInstanceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkflowInstanceId must be a non-null GUID string.");
        if (!Guid.TryParse(str, out var guid))
            throw new JsonException($"'{str}' is not a valid GUID.");
        return new WorkflowInstanceId(guid);
    }

    public override void Write(Utf8JsonWriter writer, WorkflowInstanceId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

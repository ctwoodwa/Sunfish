using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="RemodelProject"/>. UUIDv7.</summary>
[JsonConverter(typeof(RemodelProjectIdJsonConverter))]
public readonly record struct RemodelProjectId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static RemodelProjectId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(RemodelProjectId id) => id.Value;
}

internal sealed class RemodelProjectIdJsonConverter : JsonConverter<RemodelProjectId>
{
    public override RemodelProjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RemodelProjectId must be a non-null string.");
        return new RemodelProjectId(Guid.Parse(str));
    }
    public override void Write(Utf8JsonWriter writer, RemodelProjectId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

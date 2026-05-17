using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Strongly-typed identifier for a <see cref="RemodelPhase"/>. UUIDv7.</summary>
[JsonConverter(typeof(RemodelPhaseIdJsonConverter))]
public readonly record struct RemodelPhaseId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static RemodelPhaseId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(RemodelPhaseId id) => id.Value;
}

internal sealed class RemodelPhaseIdJsonConverter : JsonConverter<RemodelPhaseId>
{
    public override RemodelPhaseId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RemodelPhaseId must be a non-null string.");
        return new RemodelPhaseId(Guid.Parse(str));
    }
    public override void Write(Utf8JsonWriter writer, RemodelPhaseId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}

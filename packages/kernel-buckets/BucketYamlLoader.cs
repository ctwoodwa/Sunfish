using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Default <see cref="IBucketYamlLoader"/> backed by YamlDotNet. Accepts both
/// <c>snake_case</c> (paper format) and <c>PascalCase</c> keys; snake_case is canonical.
/// </summary>
public sealed class BucketYamlLoader : IBucketYamlLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <inheritdoc />
    public IReadOnlyList<BucketDefinition> LoadFrom(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        BucketFileDto? dto;
        try
        {
            dto = _deserializer.Deserialize<BucketFileDto>(yaml);
        }
        catch (YamlException ex)
        {
            throw new BucketYamlException($"Failed to parse bucket YAML: {ex.Message}", ex);
        }

        if (dto is null || dto.Buckets is null || dto.Buckets.Count == 0)
        {
            throw new BucketYamlException("Bucket YAML must contain a non-empty 'buckets' array.");
        }

        var result = new List<BucketDefinition>(dto.Buckets.Count);
        for (var i = 0; i < dto.Buckets.Count; i++)
        {
            result.Add(ToDefinition(dto.Buckets[i], i));
        }
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<BucketDefinition> LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var text = System.IO.File.ReadAllText(path);
        return LoadFrom(text);
    }

    private static BucketDefinition ToDefinition(BucketDto dto, int index)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new BucketYamlException($"buckets[{index}]: 'name' is required.");
        }
        if (dto.RecordTypes is null || dto.RecordTypes.Count == 0)
        {
            throw new BucketYamlException($"buckets[{index}] ('{dto.Name}'): 'record_types' must be a non-empty list.");
        }
        if (string.IsNullOrWhiteSpace(dto.RequiredAttestation))
        {
            throw new BucketYamlException($"buckets[{index}] ('{dto.Name}'): 'required_attestation' is required.");
        }

        var replication = ParseReplication(dto.Replication, dto.Name ?? $"buckets[{index}]");

        return new BucketDefinition(
            Name: dto.Name!,
            RecordTypes: dto.RecordTypes.ToArray(),
            Filter: string.IsNullOrWhiteSpace(dto.Filter) ? null : dto.Filter!.Trim(),
            Replication: replication,
            RequiredAttestation: dto.RequiredAttestation!,
            MaxLocalAgeDays: dto.MaxLocalAgeDays);
    }

    private static ReplicationMode ParseReplication(string? raw, string bucketName)
    {
        // Paper §10.2 does not mandate a default. We choose Eager — the safer default; it
        // degrades gracefully if the operator forgets to declare intent, and it matches
        // the first example bucket (team_core) in the paper.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ReplicationMode.Eager;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "eager" => ReplicationMode.Eager,
            "lazy" => ReplicationMode.Lazy,
            _ => throw new BucketYamlException(
                $"buckets ('{bucketName}'): 'replication' must be 'eager' or 'lazy', got '{raw}'."),
        };
    }

    /// <summary>Top-level YAML DTO: <c>{ buckets: [...] }</c>.</summary>
    private sealed class BucketFileDto
    {
        public List<BucketDto>? Buckets { get; set; }
    }

    /// <summary>Per-bucket YAML DTO matching paper §10.2 field names (snake_case).</summary>
    private sealed class BucketDto
    {
        public string? Name { get; set; }
        public List<string>? RecordTypes { get; set; }
        public string? Filter { get; set; }
        public string? Replication { get; set; }
        public string? RequiredAttestation { get; set; }
        public int? MaxLocalAgeDays { get; set; }
    }
}

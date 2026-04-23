namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Parses paper §10.2 YAML bucket definitions into <see cref="BucketDefinition"/> instances.
/// </summary>
/// <remarks>
/// Expected YAML shape (paper §10.2):
/// <code>
/// buckets:
///   - name: team_core
///     record_types: [projects, tasks, members, comments]
///     filter: record.team_id = peer.team_id
///     replication: eager
///     required_attestation: team_member
///
///   - name: archived_projects
///     record_types: [projects, tasks]
///     filter: project.archived = true
///     replication: lazy
///     required_attestation: team_member
///     max_local_age_days: 90
/// </code>
/// </remarks>
public interface IBucketYamlLoader
{
    /// <summary>Parse a YAML document from an in-memory string.</summary>
    /// <param name="yaml">The YAML document text.</param>
    /// <returns>The parsed bucket definitions, in document order.</returns>
    /// <exception cref="BucketYamlException">The document is missing required fields or malformed.</exception>
    IReadOnlyList<BucketDefinition> LoadFrom(string yaml);

    /// <summary>Parse a YAML document from a file path.</summary>
    /// <param name="path">Absolute or working-directory-relative path to the YAML file.</param>
    /// <returns>The parsed bucket definitions, in document order.</returns>
    /// <exception cref="BucketYamlException">The document is missing required fields or malformed.</exception>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist.</exception>
    IReadOnlyList<BucketDefinition> LoadFromFile(string path);
}

/// <summary>Thrown when a bucket YAML document cannot be parsed into valid bucket definitions.</summary>
public sealed class BucketYamlException : Exception
{
    /// <summary>Create a new <see cref="BucketYamlException"/>.</summary>
    public BucketYamlException(string message) : base(message) { }

    /// <summary>Create a new <see cref="BucketYamlException"/> wrapping a parser exception.</summary>
    public BucketYamlException(string message, Exception innerException) : base(message, innerException) { }
}

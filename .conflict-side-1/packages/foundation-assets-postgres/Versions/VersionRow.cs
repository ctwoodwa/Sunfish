namespace Sunfish.Foundation.Assets.Postgres.Versions;

/// <summary>
/// EF Core row mapped to the <c>versions</c> table.
/// </summary>
/// <remarks>
/// Composite primary key <c>(entity_scheme, entity_authority, entity_local_part, sequence)</c>.
/// The chain hash is stored in <see cref="Hash"/>; the parent link is materialized as
/// <see cref="ParentSequence"/> + <see cref="ParentHash"/> (nullable for the first version).
/// </remarks>
public sealed class VersionRow
{
    /// <summary>Scheme portion of the entity id.</summary>
    public required string EntityScheme { get; set; }

    /// <summary>Authority portion of the entity id.</summary>
    public required string EntityAuthority { get; set; }

    /// <summary>Local-part portion of the entity id.</summary>
    public required string EntityLocalPart { get; set; }

    /// <summary>Monotonic per-entity version sequence (starts at 1).</summary>
    public int Sequence { get; set; }

    /// <summary>SHA-256 hash of this version.</summary>
    public required string Hash { get; set; }

    /// <summary>Sequence of the parent version, if any.</summary>
    public int? ParentSequence { get; set; }

    /// <summary>Hash of the parent version, if any.</summary>
    public string? ParentHash { get; set; }

    /// <summary>Canonical-JSON body of this version.</summary>
    public required string BodyJson { get; set; }

    /// <summary>When this version becomes valid.</summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>When this version's validity closes (null = still open).</summary>
    public DateTimeOffset? ValidTo { get; set; }

    /// <summary>Authoring actor.</summary>
    public required string Author { get; set; }

    /// <summary>Optional signature bytes (reserved for Phase B).</summary>
    public byte[]? Signature { get; set; }

    /// <summary>Optional JSON patch for compact diff storage (unused in Phase A).</summary>
    public string? DiffJson { get; set; }
}

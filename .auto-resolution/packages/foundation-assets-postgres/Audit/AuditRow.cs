namespace Sunfish.Foundation.Assets.Postgres.Audit;

/// <summary>
/// EF Core row mapped to the <c>audit_records</c> table.
/// </summary>
/// <remarks>
/// Append-only + hash-chained per-entity. <see cref="Hash"/> is the SHA-256 computed by
/// <c>Sunfish.Foundation.Assets.Audit.HashChain.ComputeHash</c> at append time.
/// </remarks>
public sealed class AuditRow
{
    /// <summary>Surrogate identifier (sequence-generated).</summary>
    public long AuditId { get; set; }

    /// <summary>Entity scheme.</summary>
    public required string EntityScheme { get; set; }

    /// <summary>Entity authority.</summary>
    public required string EntityAuthority { get; set; }

    /// <summary>Entity local-part.</summary>
    public required string EntityLocalPart { get; set; }

    /// <summary>Related version sequence, if the op targets a specific version.</summary>
    public int? VersionSequence { get; set; }

    /// <summary>Related version hash.</summary>
    public string? VersionHash { get; set; }

    /// <summary>Audit operation code.</summary>
    public int Op { get; set; }

    /// <summary>Acting principal.</summary>
    public required string Actor { get; set; }

    /// <summary>Owning tenant.</summary>
    public required string Tenant { get; set; }

    /// <summary>Event instant.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Optional human-readable justification.</summary>
    public string? Justification { get; set; }

    /// <summary>Arbitrary event payload.</summary>
    public required string PayloadJson { get; set; }

    /// <summary>Optional signature bytes (reserved for Phase B).</summary>
    public byte[]? Signature { get; set; }

    /// <summary>Id of the previous record in the per-entity chain (null for the first).</summary>
    public long? PrevAuditId { get; set; }

    /// <summary>SHA-256 of the chain-hash function applied to this record.</summary>
    public required string Hash { get; set; }
}

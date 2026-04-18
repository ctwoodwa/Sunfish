namespace Sunfish.Foundation.Assets.Postgres.Entities;

/// <summary>
/// EF Core row mapped to the <c>entities</c> table.
/// </summary>
/// <remarks>
/// Materialized "current" projection: one row per entity with a pointer to the tip of the
/// append-only version log, plus the canonical body JSON for O(1) reads.
/// </remarks>
public sealed class EntityRow
{
    /// <summary>Scheme portion of the entity id.</summary>
    public required string EntityScheme { get; set; }

    /// <summary>Authority portion of the entity id.</summary>
    public required string EntityAuthority { get; set; }

    /// <summary>Local-part portion of the entity id.</summary>
    public required string EntityLocalPart { get; set; }

    /// <summary>Schema id that this entity's body conforms to.</summary>
    public required string Schema { get; set; }

    /// <summary>Owning tenant slug.</summary>
    public required string Tenant { get; set; }

    /// <summary>Sequence of the current version.</summary>
    public int CurrentSequence { get; set; }

    /// <summary>Hash of the current version.</summary>
    public required string CurrentHash { get; set; }

    /// <summary>Canonical-JSON body of the current version.</summary>
    public required string BodyJson { get; set; }

    /// <summary>Mint instant.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last update or tombstone instant.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the entity was tombstoned, if ever.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Nonce captured at mint time for idempotency detection.</summary>
    public string? CreationNonce { get; set; }

    /// <summary>Issuer captured at mint time.</summary>
    public required string CreationIssuer { get; set; }

    /// <summary>
    /// Postgres <c>xmin</c> system column, used as an optimistic concurrency token.
    /// </summary>
    public uint RowVersion { get; set; }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// A role-edge between a <see cref="Party"/> and a downstream consumer record
/// — "this party is the customer on order X", "this party is the tenant on
/// lease Y". Role edges are the join model; downstream blocks
/// (blocks-financial-ar, blocks-leases, etc.) do NOT denormalize Party
/// fields into their tables — they hold a <see cref="PartyId"/> and a
/// matching <see cref="PartyRole"/> edge.
///
/// <para>
/// <b>Append-only with tombstone (CRDT §2):</b> Detach is an UPDATE on the
/// row's <see cref="EndedAt"/> + <see cref="EndedReason"/>; the row is never
/// hard-deleted. Re-attaching the same party in the same role inserts a NEW
/// row with a fresh <see cref="StartedAt"/> — the old row's <see cref="EndedAt"/>
/// is never cleared. This preserves an auditable history of role tenure.
/// </para>
///
/// <para>
/// <b>RoleRecordId is opaque.</b> It's a string handle into the consumer
/// cluster's domain (e.g. an `InvoiceId` for the customer role on an
/// invoice). Strongly typing it would require this package to depend on
/// every downstream cluster, which is exactly the coupling the foundation
/// is meant to avoid. Consumers parse it themselves.
/// </para>
/// </summary>
public sealed record PartyRole
{
    /// <summary>Stable identifier.</summary>
    public required PartyRoleId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The party this role-edge attaches to.</summary>
    public required PartyId PartyId { get; init; }

    /// <summary>Role code — see <see cref="PartyRoleName"/> for canonical v1 codes. Unknown shape-valid codes are accepted (CRDT §5 open-set).</summary>
    public required string RoleName { get; init; }

    /// <summary>Opaque pointer into the consumer cluster's record (e.g. <c>InvoiceId</c> for the customer-on-invoice edge).</summary>
    public required string RoleRecordId { get; init; }

    /// <summary>When the party began holding this role.</summary>
    public required Instant StartedAt { get; init; }

    /// <summary>When the party stopped holding this role; null while active.</summary>
    public Instant? EndedAt { get; init; }

    /// <summary>Optional free-text reason for ending (e.g. "lease terminated", "vendor reclassified to contractor").</summary>
    public string? EndedReason { get; init; }

    // ── CRDT envelope ──
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAt { get; init; }
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();

    /// <summary>Construct a freshly-attached role edge.</summary>
    public static PartyRole Create(
        TenantId tenantId,
        PartyId partyId,
        string roleName,
        string roleRecordId,
        PartyId createdBy,
        PartyRoleId? id = null,
        Instant? startedAt = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new PartyRole
        {
            Id = id ?? PartyRoleId.NewId(),
            TenantId = tenantId,
            PartyId = partyId,
            RoleName = roleName,
            RoleRecordId = roleRecordId,
            StartedAt = startedAt ?? now,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            Version = 1,
        };
    }

    /// <summary>
    /// Returns a new <see cref="PartyRole"/> marking this edge as ended, with
    /// the version bumped and <see cref="UpdatedAt"/> set. The original row is
    /// not mutated — write services persist the returned record over the same
    /// <see cref="Id"/>.
    /// </summary>
    public PartyRole End(Instant endedAt, string? reason, PartyId endedBy) => this with
    {
        EndedAt = endedAt,
        EndedReason = reason,
        UpdatedAt = endedAt,
        UpdatedBy = endedBy,
        Version = Version + 1,
    };

    /// <summary>True when the role is currently held (no <see cref="EndedAt"/> set and not tombstoned).</summary>
    public bool IsActive => EndedAt is null && DeletedAt is null;
}

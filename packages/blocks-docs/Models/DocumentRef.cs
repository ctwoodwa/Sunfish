using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// Cross-cluster join entity that links a docs <see cref="Attachment"/>
/// to a parent entity in some OTHER cluster (blocks-financial-ar,
/// blocks-financial-ap, blocks-leases, blocks-inspections,
/// blocks-work-orders, …). PR 4's central deliverable.
///
/// <para>
/// <b>Why a join entity, not a foreign key.</b> Consumer clusters MUST
/// NOT take a hard reference on <c>Sunfish.Blocks.Docs.Services</c> —
/// that would put the docs cluster on the import path of every cluster
/// that owns an entity with attachments, breaking the cluster-mirror
/// boundary. Instead, the consumer cluster persists a
/// <c>(ClusterCode, ParentEntityType, ParentEntityId)</c> tuple in its
/// own write path, and the docs cluster owns the join table that
/// answers two reverse questions:
/// </para>
/// <list type="bullet">
/// <item><b>Which entities point at this attachment?</b> (e.g., is this PDF still in use anywhere?)</item>
/// <item><b>Which attachments does this entity own?</b> (e.g., what files are on this invoice?)</item>
/// </list>
///
/// <para>
/// <b>Tombstone vs hard delete.</b> Removing the link sets
/// <see cref="DeletedAtUtc"/> but keeps the row — the consumer
/// cluster's audit trail still resolves the historical link. GC of
/// orphaned <see cref="Attachment"/>s is a follow-on (PR 6) and looks
/// at <i>live</i> DocumentRefs only.
/// </para>
/// </summary>
public sealed record DocumentRef
{
    /// <summary>Stable identifier for this link.</summary>
    public required DocumentRefId Id { get; init; }

    /// <summary>Tenant scope. MUST match the linked attachment's tenant.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The attachment this link points at.</summary>
    public required AttachmentId AttachmentId { get; init; }

    /// <summary>
    /// The cluster the parent entity lives in — e.g.,
    /// <c>"blocks-financial-ar"</c>, <c>"blocks-leases"</c>. Lowercase,
    /// kebab-case, matches the cluster's NuGet package name minus the
    /// <c>Sunfish.</c> prefix.
    /// </summary>
    public required string ClusterCode { get; init; }

    /// <summary>
    /// The entity TYPE within the cluster — e.g., <c>"invoice"</c>,
    /// <c>"bill"</c>, <c>"lease"</c>, <c>"inspection"</c>. Lowercase,
    /// singular. Lets a single cluster register multiple entity kinds
    /// against the same docs-side substrate.
    /// </summary>
    public required string ParentEntityType { get; init; }

    /// <summary>
    /// The opaque id of the parent entity. Stored as a string to avoid
    /// taking a hard type-dependency on the consumer cluster's ID type.
    /// </summary>
    public required string ParentEntityId { get; init; }

    /// <summary>
    /// Optional role hint within the parent context — e.g.,
    /// <c>"primary-attachment"</c>, <c>"supporting-doc"</c>,
    /// <c>"signed-copy"</c>. Consumer clusters define their own
    /// vocabulary; this field is opaque to the docs cluster.
    /// </summary>
    public string? AttachmentRole { get; init; }

    // ── CRDT envelope ──
    public required Instant CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public Instant? DeletedAtUtc { get; init; }
    public string? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();

    /// <summary>True when this link is still live (no soft-delete tombstone).</summary>
    public bool IsLive => DeletedAtUtc is null;

    /// <summary>
    /// Construct a fresh, live DocumentRef. Callers provide the tenant
    /// + attachment + cluster-scoped parent reference; the helper fills
    /// in the CRDT envelope.
    /// </summary>
    public static DocumentRef Create(
        TenantId tenantId,
        AttachmentId attachmentId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string? createdBy = null,
        string? attachmentRole = null,
        DocumentRefId? id = null,
        Instant? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(clusterCode))
            throw new ArgumentException("ClusterCode is required.", nameof(clusterCode));
        if (string.IsNullOrWhiteSpace(parentEntityType))
            throw new ArgumentException("ParentEntityType is required.", nameof(parentEntityType));
        if (string.IsNullOrWhiteSpace(parentEntityId))
            throw new ArgumentException("ParentEntityId is required.", nameof(parentEntityId));

        var now = createdAtUtc ?? Instant.Now;
        return new DocumentRef
        {
            Id = id ?? DocumentRefId.NewId(),
            TenantId = tenantId,
            AttachmentId = attachmentId,
            ClusterCode = clusterCode,
            ParentEntityType = parentEntityType,
            ParentEntityId = parentEntityId,
            AttachmentRole = attachmentRole,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy,
            Version = 1,
        };
    }
}

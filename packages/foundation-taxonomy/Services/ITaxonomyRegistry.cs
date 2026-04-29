using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Foundation.Taxonomy.Services;

/// <summary>
/// CRUD + governance contract for the Foundation.Taxonomy substrate (ADR 0056).
/// Implementations enforce the five governance rules:
/// <list type="number">
/// <item><description><b>Authoritative regime guard:</b> only <see cref="ActorId.Sunfish"/> may mutate Authoritative-regime definitions or their nodes.</description></item>
/// <item><description><b>Node-code immutability post-publish:</b> codes are stable; renames are major-version events.</description></item>
/// <item><description><b>Tombstoning is monotonic:</b> tombstoned nodes do not return to <see cref="TaxonomyNodeStatus.Active"/>.</description></item>
/// <item><description><b>Lineage immutability:</b> a definition's <c>DerivedFrom</c> is set at creation and cannot change.</description></item>
/// <item><description><b><see cref="AlterAsync"/> requires explicit reason:</b> empty/whitespace reasons are rejected.</description></item>
/// </list>
/// </summary>
public interface ITaxonomyRegistry
{
    // ─────────────────────────── Definition lifecycle ───────────────────────────

    /// <summary>Creates a new taxonomy definition at the given version. Authoritative-regime definitions require <see cref="ActorId.Sunfish"/> as <paramref name="owner"/>.</summary>
    Task<TaxonomyDefinition> CreateAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        TaxonomyGovernanceRegime governance,
        string description,
        ActorId owner,
        TaxonomyLineage? derivedFrom,
        CancellationToken ct);

    /// <summary>Publishes a new version of an existing definition (typically with an evolved node set).</summary>
    Task<TaxonomyDefinition> PublishVersionAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion newVersion,
        ActorId publishedBy,
        CancellationToken ct);

    /// <summary>Retires a published version. Existing references continue to resolve; new references should target the latest active version.</summary>
    Task RetireDefinitionVersionAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        ActorId retiredBy,
        CancellationToken ct);

    // ────────────────────────────── Node lifecycle ──────────────────────────────

    /// <summary>Adds a node (root or child) to a definition+version pair.</summary>
    Task<TaxonomyNode> AddNodeAsync(
        TenantId tenantId,
        TaxonomyDefinitionId definition,
        TaxonomyVersion version,
        string code,
        string display,
        string description,
        string? parentCode,
        ActorId addedBy,
        CancellationToken ct);

    /// <summary>Revises a node's display label and description; appends a <see cref="DisplayHistory"/> entry preserving prior values.</summary>
    Task<TaxonomyNode> ReviseDisplayAsync(
        TenantId tenantId,
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        string newDisplay,
        string newDescription,
        string? revisionReason,
        ActorId revisedBy,
        CancellationToken ct);

    /// <summary>Tombstones a node. Tombstoning is monotonic — a tombstoned node may not return to active.</summary>
    Task TombstoneNodeAsync(
        TenantId tenantId,
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        string deprecationReason,
        string? successorCode,
        ActorId tombstonedBy,
        CancellationToken ct);

    // ───────────────────────────── Lineage operations ────────────────────────────

    /// <summary>Creates a Civilian-regime clone of an Authoritative-regime ancestor.</summary>
    Task<TaxonomyDefinition> CloneAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId clonedBy,
        string reason,
        CancellationToken ct);

    /// <summary>Creates a child taxonomy that adds new nodes to an ancestor's set.</summary>
    Task<TaxonomyDefinition> ExtendAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId extendedBy,
        string reason,
        CancellationToken ct);

    /// <summary>Creates a derivative with revisions to the ancestor's node set; <paramref name="reason"/> is required and may not be empty.</summary>
    Task<TaxonomyDefinition> AlterAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId alteredBy,
        string reason,
        CancellationToken ct);

    // ───────────────────────────────── Reads ────────────────────────────────────

    /// <summary>Reads a definition + version pair; returns null when not present.</summary>
    Task<TaxonomyDefinition?> GetDefinitionAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        CancellationToken ct);

    /// <summary>Reads all nodes (active + tombstoned) for a definition+version pair, ordered by <see cref="TaxonomyNode.PublishedAt"/>.</summary>
    Task<IReadOnlyList<TaxonomyNode>> GetNodesAsync(
        TenantId tenantId,
        TaxonomyDefinitionId definition,
        TaxonomyVersion version,
        CancellationToken ct);

    /// <summary>Reads a single node by id and version; returns null when not present.</summary>
    Task<TaxonomyNode?> GetNodeAsync(
        TenantId tenantId,
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        CancellationToken ct);

    /// <summary>Lists all definitions visible to the tenant, optionally filtered by vendor.</summary>
    Task<IReadOnlyList<TaxonomyDefinition>> ListDefinitionsAsync(
        TenantId tenantId,
        string? filterByVendor,
        CancellationToken ct);

    // ───────────────────────────── Bootstrap ─────────────────────────────────────

    /// <summary>
    /// Bulk-registers a Sunfish-shipped Authoritative taxonomy + its full node
    /// set in a single call. Idempotent: re-registration with identical data
    /// is a no-op; re-registration with different data throws
    /// <see cref="TaxonomyConflictException"/>.
    /// </summary>
    Task RegisterCorePackageAsync(
        TenantId tenantId,
        TaxonomyCorePackage package,
        CancellationToken ct);
}

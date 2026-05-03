using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>
/// Transactional composites for split / merge / re-parent operations over the hierarchy.
/// </summary>
/// <remarks>
/// Spec §8.2–§8.4. Phase A composes primitive calls in-process; Phase B will wrap them in
/// a database transaction when the Postgres backend lands.
/// </remarks>
public sealed class HierarchyOperations
{
    private readonly IEntityStore _entities;
    private readonly IHierarchyService _hierarchy;
    private readonly IAuditLog _audit;

    /// <summary>Creates the operations façade.</summary>
    public HierarchyOperations(IEntityStore entities, IHierarchyService hierarchy, IAuditLog audit)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>
    /// Splits <paramref name="oldEntity"/> into <paramref name="newEntities"/> and reassigns
    /// children according to <paramref name="childReassignments"/>.
    /// </summary>
    public async Task<SplitResult> SplitAsync(
        EntityId oldEntity,
        IReadOnlyList<SplitTarget> newEntities,
        IReadOnlyDictionary<EntityId, EntityId> childReassignments,
        string justification,
        ActorId actor,
        TenantId tenant,
        DateTimeOffset effectiveAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newEntities);
        ArgumentNullException.ThrowIfNull(childReassignments);

        // 1. Mint the new entities.
        var mintedIds = new List<EntityId>(newEntities.Count);
        foreach (var target in newEntities)
        {
            var id = await _entities.CreateAsync(target.Schema, target.Body, target.Options with { ValidFrom = effectiveAt }, ct).ConfigureAwait(false);
            mintedIds.Add(id);
        }

        // 2. Reassign existing children.
        var reassignedChildren = new List<EntityId>();
        if (_hierarchy is InMemoryHierarchyService inMem)
        {
            var active = inMem.GetOutgoingActiveChildEdges(oldEntity, effectiveAt);
            foreach (var edge in active)
            {
                if (!childReassignments.TryGetValue(edge.From, out var newParent))
                    continue; // Caller did not map this child; leave to a subsequent operation.

                await _hierarchy.InvalidateEdgeAsync(edge.Id, effectiveAt, ct).ConfigureAwait(false);
                await _hierarchy.AddEdgeAsync(edge.From, newParent, EdgeKind.ChildOf, effectiveAt, null, ct).ConfigureAwait(false);
                reassignedChildren.Add(edge.From);
            }
        }

        // 3. Mark old entity as superseded by each new one.
        foreach (var newId in mintedIds)
        {
            await _hierarchy.AddEdgeAsync(oldEntity, newId, EdgeKind.SupersededBy, effectiveAt, null, ct).ConfigureAwait(false);
        }

        // 4. Tombstone the old entity.
        await _entities.DeleteAsync(oldEntity, new DeleteOptions(actor, effectiveAt, justification), ct).ConfigureAwait(false);

        // 5. Audit.
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            op = "split",
            old = oldEntity.ToString(),
            newIds = mintedIds.Select(i => i.ToString()).ToArray(),
            reassigned = reassignedChildren.Select(i => i.ToString()).ToArray(),
        }));
        await _audit.AppendAsync(new AuditAppend(
            EntityId: oldEntity,
            VersionId: null,
            Op: Op.Split,
            Actor: actor,
            Tenant: tenant,
            At: effectiveAt,
            Payload: payload,
            Justification: justification), ct).ConfigureAwait(false);

        return new SplitResult(oldEntity, mintedIds, reassignedChildren);
    }

    /// <summary>
    /// Merges <paramref name="oldEntities"/> into a single new entity. Their children are
    /// re-parented onto the new entity.
    /// </summary>
    public async Task<MergeResult> MergeAsync(
        IReadOnlyList<EntityId> oldEntities,
        SchemaId newSchema,
        JsonDocument newBody,
        CreateOptions newOptions,
        string justification,
        ActorId actor,
        TenantId tenant,
        DateTimeOffset effectiveAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(oldEntities);
        ArgumentNullException.ThrowIfNull(newBody);

        var newId = await _entities.CreateAsync(newSchema, newBody, newOptions with { ValidFrom = effectiveAt }, ct).ConfigureAwait(false);

        var reassigned = new List<EntityId>();
        if (_hierarchy is InMemoryHierarchyService inMem)
        {
            foreach (var oldId in oldEntities)
            {
                var active = inMem.GetOutgoingActiveChildEdges(oldId, effectiveAt);
                foreach (var edge in active)
                {
                    await _hierarchy.InvalidateEdgeAsync(edge.Id, effectiveAt, ct).ConfigureAwait(false);
                    await _hierarchy.AddEdgeAsync(edge.From, newId, EdgeKind.ChildOf, effectiveAt, null, ct).ConfigureAwait(false);
                    reassigned.Add(edge.From);
                }
            }
        }

        foreach (var oldId in oldEntities)
        {
            await _hierarchy.AddEdgeAsync(oldId, newId, EdgeKind.SupersededBy, effectiveAt, null, ct).ConfigureAwait(false);
            await _entities.DeleteAsync(oldId, new DeleteOptions(actor, effectiveAt, justification), ct).ConfigureAwait(false);
        }

        var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            op = "merge",
            newId = newId.ToString(),
            oldIds = oldEntities.Select(i => i.ToString()).ToArray(),
            reassigned = reassigned.Select(i => i.ToString()).ToArray(),
        }));
        await _audit.AppendAsync(new AuditAppend(
            EntityId: newId,
            VersionId: null,
            Op: Op.Merge,
            Actor: actor,
            Tenant: tenant,
            At: effectiveAt,
            Payload: payload,
            Justification: justification), ct).ConfigureAwait(false);

        return new MergeResult(newId, oldEntities, reassigned);
    }

    /// <summary>Re-parents <paramref name="child"/> from one parent to another.</summary>
    public async Task ReparentAsync(
        EntityId child,
        EntityId oldParent,
        EntityId newParent,
        string justification,
        ActorId actor,
        TenantId tenant,
        DateTimeOffset effectiveAt,
        CancellationToken ct = default)
    {
        if (_hierarchy is InMemoryHierarchyService inMem)
        {
            var active = inMem.GetOutgoingActiveChildEdges(oldParent, effectiveAt)
                .Where(e => e.From == child)
                .ToList();
            foreach (var edge in active)
            {
                await _hierarchy.InvalidateEdgeAsync(edge.Id, effectiveAt, ct).ConfigureAwait(false);
            }
        }

        await _hierarchy.AddEdgeAsync(child, newParent, EdgeKind.ChildOf, effectiveAt, null, ct).ConfigureAwait(false);

        var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            op = "reparent",
            child = child.ToString(),
            oldParent = oldParent.ToString(),
            newParent = newParent.ToString(),
        }));
        await _audit.AppendAsync(new AuditAppend(
            EntityId: child,
            VersionId: null,
            Op: Op.Reparent,
            Actor: actor,
            Tenant: tenant,
            At: effectiveAt,
            Payload: payload,
            Justification: justification), ct).ConfigureAwait(false);
    }
}

/// <summary>Input for a single split target (new entity to mint as part of a split).</summary>
public sealed record SplitTarget(SchemaId Schema, JsonDocument Body, CreateOptions Options);

/// <summary>Return value of <see cref="HierarchyOperations.SplitAsync"/>.</summary>
public sealed record SplitResult(
    EntityId OldEntity,
    IReadOnlyList<EntityId> NewEntities,
    IReadOnlyList<EntityId> ReassignedChildren);

/// <summary>Return value of <see cref="HierarchyOperations.MergeAsync"/>.</summary>
public sealed record MergeResult(
    EntityId NewEntity,
    IReadOnlyList<EntityId> OldEntities,
    IReadOnlyList<EntityId> ReassignedChildren);

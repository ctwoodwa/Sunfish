using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Temporal;

namespace Sunfish.Foundation.Assets.Postgres.Hierarchy;

/// <summary>
/// EF Core + PostgreSQL <see cref="IHierarchyService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Persists a closure-table schema plus the temporal <c>hierarchy_edges</c> table.
/// On <c>AddEdgeAsync</c>, new closure rows are computed incrementally by the classical
/// closure algorithm: for every open ancestor of the new edge's target and every open
/// descendant of the new edge's source, insert a row at <c>depth(anc→to) + 1 + depth(from→desc)</c>.
/// </para>
/// <para>
/// On <c>InvalidateEdgeAsync</c>, every currently-open closure row whose ancestor is an
/// ancestor of the removed edge's target and whose descendant is a descendant of the
/// removed edge's source is closed at the invalidation instant. Cycle detection prevents
/// <c>AddEdgeAsync</c> from introducing a path back to an ancestor of the new child.
/// </para>
/// </remarks>
public sealed class PostgresHierarchyService : IHierarchyService
{
    private readonly IDbContextFactory<AssetStoreDbContext> _factory;

    /// <summary>Creates the service.</summary>
    public PostgresHierarchyService(IDbContextFactory<AssetStoreDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async Task<EntityEdge> AddEdgeAsync(
        EntityId from,
        EntityId to,
        EdgeKind kind,
        DateTimeOffset validFrom,
        JsonDocument? metadata = null,
        CancellationToken ct = default)
    {
        var effective = validFrom.ToUniversalTime();

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var edge = new EdgeRow
        {
            FromScheme = from.Scheme,
            FromAuthority = from.Authority,
            FromLocalPart = from.LocalPart,
            ToScheme = to.Scheme,
            ToAuthority = to.Authority,
            ToLocalPart = to.LocalPart,
            Kind = (int)kind,
            ValidFrom = effective,
            ValidTo = null,
            MetadataJson = metadata is null ? null : JsonCanonicalizer.ToCanonicalString(metadata),
        };
        db.HierarchyEdges.Add(edge);

        if (kind == EdgeKind.ChildOf)
        {
            // Cycle detection: if `to` is already a descendant of `from` at `effective`,
            // adding `from → to` (child=from, parent=to) would close a cycle.
            if (from.Equals(to))
            {
                throw new InvalidOperationException("Self-edge (child == parent) would create a cycle.");
            }

            var toIsDescendantOfFrom = await db.HierarchyClosure
                .AnyAsync(
                    c => c.AncestorScheme == from.Scheme &&
                         c.AncestorAuthority == from.Authority &&
                         c.AncestorLocalPart == from.LocalPart &&
                         c.DescendantScheme == to.Scheme &&
                         c.DescendantAuthority == to.Authority &&
                         c.DescendantLocalPart == to.LocalPart &&
                         c.Depth > 0 &&
                         c.ValidFrom <= effective &&
                         (c.ValidTo == null || c.ValidTo > effective),
                    ct)
                .ConfigureAwait(false);
            if (toIsDescendantOfFrom)
            {
                throw new InvalidOperationException(
                    $"Adding edge {from} → {to} would create a cycle in the hierarchy.");
            }

            // Ensure self-rows for both endpoints exist *before* we compute closure rows,
            // so queries see them. Self-row validity is unbounded in both directions.
            await EnsureSelfRowPersistedAsync(db, from, ct).ConfigureAwait(false);
            await EnsureSelfRowPersistedAsync(db, to, ct).ConfigureAwait(false);

            // Ancestors-of-parent from the database (still-open at `effective`).
            var parentAncestors = await db.HierarchyClosure.AsNoTracking()
                .Where(c => c.DescendantScheme == to.Scheme &&
                            c.DescendantAuthority == to.Authority &&
                            c.DescendantLocalPart == to.LocalPart &&
                            c.ValidFrom <= effective &&
                            (c.ValidTo == null || c.ValidTo > effective))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Descendants-of-child from the database (still-open at `effective`).
            var childDescendants = await db.HierarchyClosure.AsNoTracking()
                .Where(c => c.AncestorScheme == from.Scheme &&
                            c.AncestorAuthority == from.Authority &&
                            c.AncestorLocalPart == from.LocalPart &&
                            c.ValidFrom <= effective &&
                            (c.ValidTo == null || c.ValidTo > effective))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Cross-product: for every ancestor of `to` and every descendant of `from`,
            // open a new closure row at depth(ancestor→to) + 1 + depth(from→descendant).
            foreach (var pa in parentAncestors)
            {
                foreach (var cd in childDescendants)
                {
                    var newDepth = pa.Depth + 1 + cd.Depth;

                    var dbExists = await db.HierarchyClosure.AsNoTracking().AnyAsync(
                        c => c.AncestorScheme == pa.AncestorScheme &&
                             c.AncestorAuthority == pa.AncestorAuthority &&
                             c.AncestorLocalPart == pa.AncestorLocalPart &&
                             c.DescendantScheme == cd.DescendantScheme &&
                             c.DescendantAuthority == cd.DescendantAuthority &&
                             c.DescendantLocalPart == cd.DescendantLocalPart &&
                             c.Depth == newDepth &&
                             c.ValidFrom <= effective &&
                             (c.ValidTo == null || c.ValidTo > effective),
                        ct).ConfigureAwait(false);
                    if (dbExists) continue;

                    db.HierarchyClosure.Add(new ClosureRow
                    {
                        AncestorScheme = pa.AncestorScheme,
                        AncestorAuthority = pa.AncestorAuthority,
                        AncestorLocalPart = pa.AncestorLocalPart,
                        DescendantScheme = cd.DescendantScheme,
                        DescendantAuthority = cd.DescendantAuthority,
                        DescendantLocalPart = cd.DescendantLocalPart,
                        Depth = newDepth,
                        ValidFrom = effective,
                        ValidTo = null,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return ToDomainEdge(edge);
    }

    /// <inheritdoc />
    public async Task InvalidateEdgeAsync(long edgeId, DateTimeOffset validTo, CancellationToken ct = default)
    {
        var effective = validTo.ToUniversalTime();

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var edge = await db.HierarchyEdges.FirstOrDefaultAsync(e => e.EdgeId == edgeId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Edge {edgeId} not found.");
        if (edge.ValidTo is not null) return;

        edge.ValidTo = effective;

        if (edge.Kind == (int)EdgeKind.ChildOf)
        {
            var fromId = new EntityId(edge.FromScheme, edge.FromAuthority, edge.FromLocalPart);
            var toId = new EntityId(edge.ToScheme, edge.ToAuthority, edge.ToLocalPart);

            // Ancestors-of-parent at `effective`.
            var ancestorsOfParent = await db.HierarchyClosure.AsNoTracking()
                .Where(c => c.DescendantScheme == toId.Scheme &&
                            c.DescendantAuthority == toId.Authority &&
                            c.DescendantLocalPart == toId.LocalPart &&
                            c.ValidFrom <= effective &&
                            (c.ValidTo == null || c.ValidTo > effective))
                .Select(c => new { c.AncestorScheme, c.AncestorAuthority, c.AncestorLocalPart })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Descendants-of-child at `effective`.
            var descendantsOfChild = await db.HierarchyClosure.AsNoTracking()
                .Where(c => c.AncestorScheme == fromId.Scheme &&
                            c.AncestorAuthority == fromId.Authority &&
                            c.AncestorLocalPart == fromId.LocalPart &&
                            c.ValidFrom <= effective &&
                            (c.ValidTo == null || c.ValidTo > effective))
                .Select(c => new { c.DescendantScheme, c.DescendantAuthority, c.DescendantLocalPart })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var ancestorSet = ancestorsOfParent
                .Select(a => (a.AncestorScheme, a.AncestorAuthority, a.AncestorLocalPart))
                .ToHashSet();
            var descendantSet = descendantsOfChild
                .Select(d => (d.DescendantScheme, d.DescendantAuthority, d.DescendantLocalPart))
                .ToHashSet();

            if (ancestorSet.Count > 0 && descendantSet.Count > 0)
            {
                // Pull still-open closure rows that match the ancestor/descendant cross-product.
                var ancestorSchemes = ancestorSet.Select(a => a.AncestorScheme).ToArray();
                var descendantSchemes = descendantSet.Select(d => d.DescendantScheme).ToArray();
                var candidateRows = await db.HierarchyClosure
                    .Where(c => c.Depth > 0 &&
                                c.ValidTo == null &&
                                ancestorSchemes.Contains(c.AncestorScheme) &&
                                descendantSchemes.Contains(c.DescendantScheme))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var row in candidateRows)
                {
                    var anc = (row.AncestorScheme, row.AncestorAuthority, row.AncestorLocalPart);
                    var desc = (row.DescendantScheme, row.DescendantAuthority, row.DescendantLocalPart);
                    if (!ancestorSet.Contains(anc)) continue;
                    if (!descendantSet.Contains(desc)) continue;
                    row.ValidTo = effective;
                }
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EntityEdge> GetChildrenAsync(
        EntityId parent,
        DateTimeOffset? asOf = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = (asOf ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyEdges.AsNoTracking()
            .Where(e => e.Kind == (int)EdgeKind.ChildOf &&
                        e.ToScheme == parent.Scheme &&
                        e.ToAuthority == parent.Authority &&
                        e.ToLocalPart == parent.LocalPart &&
                        e.ValidFrom <= t &&
                        (e.ValidTo == null || e.ValidTo > t))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return ToDomainEdge(row);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EntityEdge> GetParentsAsync(
        EntityId child,
        DateTimeOffset? asOf = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = (asOf ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyEdges.AsNoTracking()
            .Where(e => e.Kind == (int)EdgeKind.ChildOf &&
                        e.FromScheme == child.Scheme &&
                        e.FromAuthority == child.Authority &&
                        e.FromLocalPart == child.LocalPart &&
                        e.ValidFrom <= t &&
                        (e.ValidTo == null || e.ValidTo > t))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return ToDomainEdge(row);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ClosureEntry> GetAncestorsAsync(
        EntityId descendant,
        DateTimeOffset? asOf = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = (asOf ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyClosure.AsNoTracking()
            .Where(c => c.DescendantScheme == descendant.Scheme &&
                        c.DescendantAuthority == descendant.Authority &&
                        c.DescendantLocalPart == descendant.LocalPart &&
                        c.Depth > 0 &&
                        c.ValidFrom <= t &&
                        (c.ValidTo == null || c.ValidTo > t))
            .OrderBy(c => c.Depth)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return ToDomainClosure(row);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ClosureEntry> GetDescendantsAsync(
        EntityId ancestor,
        DateTimeOffset? asOf = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = (asOf ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyClosure.AsNoTracking()
            .Where(c => c.AncestorScheme == ancestor.Scheme &&
                        c.AncestorAuthority == ancestor.Authority &&
                        c.AncestorLocalPart == ancestor.LocalPart &&
                        c.Depth > 0 &&
                        c.ValidFrom <= t &&
                        (c.ValidTo == null || c.ValidTo > t))
            .OrderBy(c => c.Depth)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return ToDomainClosure(row);
        }
    }

    /// <inheritdoc />
    public async Task<TemporalSnapshot> GetSubtreeAsync(
        EntityId root,
        DateTimeOffset? asOf = null,
        CancellationToken ct = default)
    {
        var t = (asOf ?? DateTimeOffset.UtcNow).ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyClosure.AsNoTracking()
            .Where(c => c.AncestorScheme == root.Scheme &&
                        c.AncestorAuthority == root.Authority &&
                        c.AncestorLocalPart == root.LocalPart &&
                        c.ValidFrom <= t &&
                        (c.ValidTo == null || c.ValidTo > t))
            .OrderBy(c => c.Depth)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var entries = rows.Select(ToDomainClosure).ToList();
        return new TemporalSnapshot(root, t, entries);
    }

    /// <summary>
    /// Returns the still-open <see cref="EdgeKind.ChildOf"/> edges whose target is
    /// <paramref name="parent"/> at <paramref name="at"/>. Exposed so
    /// <c>HierarchyOperations</c> can drive Split/Merge/Reparent transactions against
    /// the Postgres backend.
    /// </summary>
    public async Task<IReadOnlyList<EntityEdge>> GetOutgoingActiveChildEdgesAsync(
        EntityId parent,
        DateTimeOffset at,
        CancellationToken ct = default)
    {
        var t = at.ToUniversalTime();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.HierarchyEdges.AsNoTracking()
            .Where(e => e.Kind == (int)EdgeKind.ChildOf &&
                        e.ToScheme == parent.Scheme &&
                        e.ToAuthority == parent.Authority &&
                        e.ToLocalPart == parent.LocalPart &&
                        e.ValidFrom <= t &&
                        (e.ValidTo == null || e.ValidTo > t))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(ToDomainEdge).ToList();
    }

    /// <summary>
    /// Ensures a self-row <c>(entity, entity, 0)</c> exists in the closure table and
    /// <b>flushes it to the database</b> so subsequent queries within the same transaction
    /// observe it. Idempotent: returns immediately if one already exists.
    /// </summary>
    private static async Task EnsureSelfRowPersistedAsync(
        AssetStoreDbContext db,
        EntityId entity,
        CancellationToken ct)
    {
        var exists = await db.HierarchyClosure.AsNoTracking().AnyAsync(
            c => c.AncestorScheme == entity.Scheme &&
                 c.AncestorAuthority == entity.Authority &&
                 c.AncestorLocalPart == entity.LocalPart &&
                 c.DescendantScheme == entity.Scheme &&
                 c.DescendantAuthority == entity.Authority &&
                 c.DescendantLocalPart == entity.LocalPart &&
                 c.Depth == 0,
            ct).ConfigureAwait(false);
        if (exists) return;

        db.HierarchyClosure.Add(new ClosureRow
        {
            AncestorScheme = entity.Scheme,
            AncestorAuthority = entity.Authority,
            AncestorLocalPart = entity.LocalPart,
            DescendantScheme = entity.Scheme,
            DescendantAuthority = entity.Authority,
            DescendantLocalPart = entity.LocalPart,
            Depth = 0,
            ValidFrom = DateTimeOffset.MinValue.ToUniversalTime(),
            ValidTo = null,
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    internal static EntityEdge ToDomainEdge(EdgeRow row)
    {
        var from = new EntityId(row.FromScheme, row.FromAuthority, row.FromLocalPart);
        var to = new EntityId(row.ToScheme, row.ToAuthority, row.ToLocalPart);
        JsonDocument? metadata = row.MetadataJson is null ? null : JsonDocument.Parse(row.MetadataJson);
        return new EntityEdge(
            row.EdgeId,
            from,
            to,
            (EdgeKind)row.Kind,
            new TemporalRange(row.ValidFrom, row.ValidTo),
            metadata);
    }

    internal static ClosureEntry ToDomainClosure(ClosureRow row)
    {
        var ancestor = new EntityId(row.AncestorScheme, row.AncestorAuthority, row.AncestorLocalPart);
        var descendant = new EntityId(row.DescendantScheme, row.DescendantAuthority, row.DescendantLocalPart);
        return new ClosureEntry(ancestor, descendant, row.Depth, new TemporalRange(row.ValidFrom, row.ValidTo));
    }
}

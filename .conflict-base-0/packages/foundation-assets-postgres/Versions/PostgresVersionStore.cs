using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Postgres.Entities;
using Sunfish.Foundation.Assets.Versions;
using Version = Sunfish.Foundation.Assets.Versions.Version;

namespace Sunfish.Foundation.Assets.Postgres.Versions;

/// <summary>
/// EF Core + PostgreSQL <see cref="IVersionStore"/>.
/// </summary>
/// <remarks>
/// Reads are indexed by the composite (entity, sequence) primary key plus a
/// <c>(entity, valid_from)</c> index that supports efficient as-of lookups.
/// <see cref="BranchAsync"/> and <see cref="MergeAsync"/> are Phase-B-only stubs.
/// </remarks>
public sealed class PostgresVersionStore : IVersionStore
{
    private readonly IDbContextFactory<AssetStoreDbContext> _factory;

    /// <summary>Creates the store.</summary>
    public PostgresVersionStore(IDbContextFactory<AssetStoreDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async Task<Version?> GetVersionAsync(VersionId id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.Versions.AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.EntityScheme == id.Entity.Scheme &&
                     v.EntityAuthority == id.Entity.Authority &&
                     v.EntityLocalPart == id.Entity.LocalPart &&
                     v.Sequence == id.Sequence &&
                     v.Hash == id.Hash,
                ct)
            .ConfigureAwait(false);
        return row is null ? null : PostgresEntityStore.ToDomainVersion(row);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Version> GetHistoryAsync(
        EntityId entity,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.Versions.AsNoTracking()
            .Where(v => v.EntityScheme == entity.Scheme &&
                        v.EntityAuthority == entity.Authority &&
                        v.EntityLocalPart == entity.LocalPart)
            .OrderBy(v => v.Sequence)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return PostgresEntityStore.ToDomainVersion(row);
        }
    }

    /// <inheritdoc />
    public async Task<Version?> GetAsOfAsync(EntityId entity, DateTimeOffset at, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.Versions.AsNoTracking()
            .Where(v => v.EntityScheme == entity.Scheme &&
                        v.EntityAuthority == entity.Authority &&
                        v.EntityLocalPart == entity.LocalPart &&
                        v.ValidFrom <= at &&
                        (v.ValidTo == null || v.ValidTo > at))
            .OrderByDescending(v => v.Sequence)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return row is null ? null : PostgresEntityStore.ToDomainVersion(row);
    }

    /// <inheritdoc />
    public Task<VersionId> BranchAsync(VersionId from, BranchOptions options, CancellationToken ct = default)
        => throw new NotImplementedException(
            "Phase A ships linear history only. Branch/merge lands in Platform Phase B; see plan D-CRDT-ROUTE.");

    /// <inheritdoc />
    public Task<VersionId> MergeAsync(VersionId left, VersionId right, MergeOptions options, CancellationToken ct = default)
        => throw new NotImplementedException(
            "Phase A ships linear history only. Branch/merge lands in Platform Phase B; see plan D-CRDT-ROUTE.");
}

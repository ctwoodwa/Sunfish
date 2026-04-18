using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Postgres.Internal;
using Sunfish.Foundation.Assets.Postgres.Versions;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Assets.Postgres.Entities;

/// <summary>
/// EF Core + PostgreSQL <see cref="IEntityStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores each <see cref="EntityId"/> across three separate columns so joins and indexes
/// are natural. Writes use Postgres' <c>xmin</c> system column as an optimistic concurrency
/// token (plan D-VERSION-STORE-SHAPE) so concurrent mutations fail fast instead of silently
/// clobbering.
/// </para>
/// <para>
/// Every mutating call runs inside a <see cref="IsolationLevel.Serializable"/> transaction
/// so the materialized current-body cache and the append-only version log stay in sync
/// under parallel workloads.
/// </para>
/// </remarks>
public sealed class PostgresEntityStore : IEntityStore
{
    private readonly IDbContextFactory<AssetStoreDbContext> _factory;
    private readonly IEntityValidator _validator;
    private readonly IVersionObserver _observer;

    /// <summary>Creates the store.</summary>
    public PostgresEntityStore(
        IDbContextFactory<AssetStoreDbContext> factory,
        IEntityValidator? validator = null,
        IVersionObserver? observer = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _validator = validator ?? NullEntityValidator.Instance;
        _observer = observer ?? NullVersionObserver.Instance;
    }

    /// <inheritdoc />
    public async Task<Entity?> GetAsync(EntityId id, VersionSelector version = default, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entity = await db.Entities.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.EntityScheme == id.Scheme &&
                     e.EntityAuthority == id.Authority &&
                     e.EntityLocalPart == id.LocalPart,
                ct)
            .ConfigureAwait(false);
        if (entity is null) return null;

        VersionRow? selected;
        if (version.ExplicitSequence is { } seq)
        {
            selected = await db.Versions.AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.EntityScheme == id.Scheme &&
                         v.EntityAuthority == id.Authority &&
                         v.EntityLocalPart == id.LocalPart &&
                         v.Sequence == seq,
                    ct)
                .ConfigureAwait(false);
            if (selected is null) return null;
        }
        else if (version.AsOf is { } at)
        {
            selected = await db.Versions.AsNoTracking()
                .Where(v => v.EntityScheme == id.Scheme &&
                            v.EntityAuthority == id.Authority &&
                            v.EntityLocalPart == id.LocalPart &&
                            v.ValidFrom <= at &&
                            (v.ValidTo == null || v.ValidTo > at))
                .OrderByDescending(v => v.Sequence)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (selected is null) return null;
            if (IsTombstone(selected.BodyJson)) return null;
        }
        else
        {
            if (entity.DeletedAt is not null) return null;
            selected = await db.Versions.AsNoTracking()
                .Where(v => v.EntityScheme == id.Scheme &&
                            v.EntityAuthority == id.Authority &&
                            v.EntityLocalPart == id.LocalPart)
                .OrderByDescending(v => v.Sequence)
                .FirstAsync(ct)
                .ConfigureAwait(false);
        }

        return Materialize(entity, selected);
    }

    /// <inheritdoc />
    public async Task<EntityId> CreateAsync(
        SchemaId schema,
        JsonDocument body,
        CreateOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(options);
        await _validator.ValidateAsync(schema, body, ct).ConfigureAwait(false);

        var id = PostgresHashing.DeriveEntityId(schema, options);
        var validFrom = NormalizeUtc(options.ValidFrom ?? DateTimeOffset.UtcNow);
        var canonicalBody = PostgresHashing.CanonicalizeBody(body);
        var hash = PostgresHashing.HashVersion(parentHash: null, canonicalBody: canonicalBody, validFrom: validFrom);

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var existing = await db.Entities
            .FirstOrDefaultAsync(
                e => e.EntityScheme == id.Scheme &&
                     e.EntityAuthority == id.Authority &&
                     e.EntityLocalPart == id.LocalPart,
                ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (string.Equals(existing.BodyJson, canonicalBody, StringComparison.Ordinal))
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return id;
            }
            throw new IdempotencyConflictException(
                $"Entity '{id}' already exists with a different body; refusing to overwrite via CreateAsync.");
        }

        var versionRow = new VersionRow
        {
            EntityScheme = id.Scheme,
            EntityAuthority = id.Authority,
            EntityLocalPart = id.LocalPart,
            Sequence = 1,
            Hash = hash,
            ParentSequence = null,
            ParentHash = null,
            BodyJson = canonicalBody,
            ValidFrom = validFrom,
            ValidTo = null,
            Author = options.Issuer.Value,
            Signature = null,
            DiffJson = null,
        };
        var entityRow = new EntityRow
        {
            EntityScheme = id.Scheme,
            EntityAuthority = id.Authority,
            EntityLocalPart = id.LocalPart,
            Schema = schema.Value,
            Tenant = options.Tenant.Value,
            CurrentSequence = 1,
            CurrentHash = hash,
            BodyJson = canonicalBody,
            CreatedAt = validFrom,
            UpdatedAt = validFrom,
            DeletedAt = null,
            CreationNonce = options.Nonce,
            CreationIssuer = options.Issuer.Value,
        };

        db.Versions.Add(versionRow);
        db.Entities.Add(entityRow);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Race: someone else minted the same id concurrently.
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw new ConcurrencyException(
                $"Entity '{id}' was created concurrently; retry the operation or use idempotent semantics.");
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        var version = ToDomainVersion(versionRow);
        await _observer.OnVersionAppendedAsync(id, version, ct).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc />
    public async Task<VersionId> UpdateAsync(
        EntityId id,
        JsonDocument newBody,
        UpdateOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newBody);
        ArgumentNullException.ThrowIfNull(options);

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var entity = await db.Entities
            .FirstOrDefaultAsync(
                e => e.EntityScheme == id.Scheme &&
                     e.EntityAuthority == id.Authority &&
                     e.EntityLocalPart == id.LocalPart,
                ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity '{id}' not found.");

        await _validator.ValidateAsync(new SchemaId(entity.Schema), newBody, ct).ConfigureAwait(false);

        var tail = await db.Versions
            .Where(v => v.EntityScheme == id.Scheme &&
                        v.EntityAuthority == id.Authority &&
                        v.EntityLocalPart == id.LocalPart)
            .OrderByDescending(v => v.Sequence)
            .FirstAsync(ct)
            .ConfigureAwait(false);

        if (options.ExpectedVersion is { } expected &&
            (tail.Sequence != expected.Sequence ||
             !string.Equals(tail.Hash, expected.Hash, StringComparison.Ordinal)))
        {
            throw new ConcurrencyException(
                $"Entity '{id}' has advanced beyond expected version {expected}; current tip is sequence {tail.Sequence}.");
        }

        var validFrom = NormalizeUtc(options.ValidFrom ?? DateTimeOffset.UtcNow);
        var canonicalBody = PostgresHashing.CanonicalizeBody(newBody);
        var hash = PostgresHashing.HashVersion(parentHash: tail.Hash, canonicalBody: canonicalBody, validFrom: validFrom);

        tail.ValidTo = validFrom;

        var appended = new VersionRow
        {
            EntityScheme = id.Scheme,
            EntityAuthority = id.Authority,
            EntityLocalPart = id.LocalPart,
            Sequence = tail.Sequence + 1,
            Hash = hash,
            ParentSequence = tail.Sequence,
            ParentHash = tail.Hash,
            BodyJson = canonicalBody,
            ValidFrom = validFrom,
            ValidTo = null,
            Author = options.Actor.Value,
            Signature = null,
            DiffJson = null,
        };
        db.Versions.Add(appended);

        entity.CurrentSequence = appended.Sequence;
        entity.CurrentHash = appended.Hash;
        entity.BodyJson = canonicalBody;
        entity.UpdatedAt = validFrom;
        entity.DeletedAt = null;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw new ConcurrencyException(
                $"Entity '{id}' was mutated concurrently (xmin concurrency guard).") { HelpLink = ex.Message };
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw new ConcurrencyException(
                $"Entity '{id}' could not be updated (version-log constraint violated).") { HelpLink = ex.Message };
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        var version = ToDomainVersion(appended);
        await _observer.OnVersionAppendedAsync(id, version, ct).ConfigureAwait(false);
        return new VersionId(id, appended.Sequence, appended.Hash);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(EntityId id, DeleteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var entity = await db.Entities
            .FirstOrDefaultAsync(
                e => e.EntityScheme == id.Scheme &&
                     e.EntityAuthority == id.Authority &&
                     e.EntityLocalPart == id.LocalPart,
                ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity '{id}' not found.");

        var tail = await db.Versions
            .Where(v => v.EntityScheme == id.Scheme &&
                        v.EntityAuthority == id.Authority &&
                        v.EntityLocalPart == id.LocalPart)
            .OrderByDescending(v => v.Sequence)
            .FirstAsync(ct)
            .ConfigureAwait(false);

        var validFrom = NormalizeUtc(options.ValidFrom ?? DateTimeOffset.UtcNow);
        var tombstoneJson = "{}";
        var hash = PostgresHashing.HashVersion(parentHash: tail.Hash, canonicalBody: tombstoneJson, validFrom: validFrom);

        tail.ValidTo = validFrom;

        var appended = new VersionRow
        {
            EntityScheme = id.Scheme,
            EntityAuthority = id.Authority,
            EntityLocalPart = id.LocalPart,
            Sequence = tail.Sequence + 1,
            Hash = hash,
            ParentSequence = tail.Sequence,
            ParentHash = tail.Hash,
            BodyJson = tombstoneJson,
            ValidFrom = validFrom,
            ValidTo = null,
            Author = options.Actor.Value,
            Signature = null,
            DiffJson = null,
        };
        db.Versions.Add(appended);

        entity.CurrentSequence = appended.Sequence;
        entity.CurrentHash = appended.Hash;
        entity.BodyJson = tombstoneJson;
        entity.UpdatedAt = validFrom;
        entity.DeletedAt = validFrom;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        var version = ToDomainVersion(appended);
        await _observer.OnVersionAppendedAsync(id, version, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Entity> QueryAsync(EntityQuery query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        IQueryable<EntityRow> q = db.Entities.AsNoTracking();
        if (query.Schema is { } schema)
            q = q.Where(e => e.Schema == schema.Value);
        if (query.Tenant is { } tenant)
            q = q.Where(e => e.Tenant == tenant.Value);

        if (!query.IncludeDeleted)
        {
            if (query.AsOf is { } asOf)
                q = q.Where(e => e.DeletedAt == null || e.DeletedAt > asOf);
            else
                q = q.Where(e => e.DeletedAt == null);
        }

        var entities = await q.ToListAsync(ct).ConfigureAwait(false);
        int emitted = 0;
        foreach (var entity in entities)
        {
            ct.ThrowIfCancellationRequested();

            VersionRow? picked;
            if (query.AsOf is { } at)
            {
                picked = await db.Versions.AsNoTracking()
                    .Where(v => v.EntityScheme == entity.EntityScheme &&
                                v.EntityAuthority == entity.EntityAuthority &&
                                v.EntityLocalPart == entity.EntityLocalPart &&
                                v.ValidFrom <= at &&
                                (v.ValidTo == null || v.ValidTo > at))
                    .OrderByDescending(v => v.Sequence)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
                if (picked is null) continue;
            }
            else
            {
                picked = await db.Versions.AsNoTracking()
                    .Where(v => v.EntityScheme == entity.EntityScheme &&
                                v.EntityAuthority == entity.EntityAuthority &&
                                v.EntityLocalPart == entity.EntityLocalPart)
                    .OrderByDescending(v => v.Sequence)
                    .FirstAsync(ct)
                    .ConfigureAwait(false);
            }

            yield return Materialize(entity, picked);
            emitted++;
            if (query.Limit is { } limit && emitted >= limit) yield break;
        }
    }

    internal static Entity Materialize(EntityRow entity, VersionRow version)
    {
        var body = JsonDocument.Parse(version.BodyJson);
        var entityId = new EntityId(entity.EntityScheme, entity.EntityAuthority, entity.EntityLocalPart);
        var versionId = new VersionId(entityId, version.Sequence, version.Hash);
        return new Entity(
            entityId,
            new SchemaId(entity.Schema),
            new TenantId(entity.Tenant),
            versionId,
            body,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.DeletedAt);
    }

    internal static Sunfish.Foundation.Assets.Versions.Version ToDomainVersion(VersionRow row)
    {
        var entityId = new EntityId(row.EntityScheme, row.EntityAuthority, row.EntityLocalPart);
        var versionId = new VersionId(entityId, row.Sequence, row.Hash);
        var parentId = row.ParentSequence is { } ps && row.ParentHash is { } ph
            ? new VersionId?(new VersionId(entityId, ps, ph))
            : null;
        var body = JsonDocument.Parse(row.BodyJson);
        JsonDocument? diff = row.DiffJson is null ? null : JsonDocument.Parse(row.DiffJson);
        return new Sunfish.Foundation.Assets.Versions.Version(
            versionId,
            parentId,
            body,
            row.ValidFrom,
            row.ValidTo,
            new ActorId(row.Author),
            row.Signature,
            diff);
    }

    private static bool IsTombstone(string canonicalBody)
        => string.Equals(canonicalBody, "{}", StringComparison.Ordinal);

    /// <summary>
    /// Truncates a timestamp to microsecond precision so the hash of a version is stable
    /// across the Postgres <c>timestamptz</c> round-trip (µs precision on the server).
    /// </summary>
    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        const long ticksPerMicrosecond = 10;
        var extraTicks = utc.Ticks % ticksPerMicrosecond;
        return extraTicks == 0 ? utc : utc.AddTicks(-extraTicks);
    }
}

using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Postgres.Audit;

/// <summary>
/// EF Core + PostgreSQL <see cref="IAuditLog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per-entity appends are serialized through a <c>Serializable</c> transaction: the new
/// row reads the current tail, computes the chain hash against it, and inserts, all in one
/// unit. Concurrent appenders for the same entity observe the new tail via the transaction
/// isolation level and either retry or fail; no interleaved chain is possible.
/// </para>
/// <para>
/// <see cref="VerifyChainAsync"/> re-runs <c>HashChain.Verify</c> against the persisted rows,
/// detecting any tampering at read time (plan D-AUDIT-CHAIN).
/// </para>
/// </remarks>
public sealed class PostgresAuditLog : IAuditLog
{
    private readonly IDbContextFactory<AssetStoreDbContext> _factory;

    /// <summary>Creates the audit log.</summary>
    public PostgresAuditLog(IDbContextFactory<AssetStoreDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async Task<AuditId> AppendAsync(AuditAppend append, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(append);

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var prev = await db.AuditRecords
            .Where(a => a.EntityScheme == append.EntityId.Scheme &&
                        a.EntityAuthority == append.EntityId.Authority &&
                        a.EntityLocalPart == append.EntityId.LocalPart)
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.AuditId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Postgres `timestamptz` stores microsecond precision; truncate the caller's
        // timestamp to microseconds *before* hashing so read-time verification sees the
        // same bytes that were written.
        var atUtc = TruncateToMicroseconds(append.At.ToUniversalTime());

        var hash = HashChain.ComputeHash(
            prev?.Hash,
            append.EntityId,
            append.Op,
            append.Actor,
            append.Tenant,
            atUtc,
            append.Payload);

        var payloadCanonical = JsonCanonicalizer.ToCanonicalString(append.Payload);

        var row = new AuditRow
        {
            EntityScheme = append.EntityId.Scheme,
            EntityAuthority = append.EntityId.Authority,
            EntityLocalPart = append.EntityId.LocalPart,
            VersionSequence = append.VersionId?.Sequence,
            VersionHash = append.VersionId?.Hash,
            Op = (int)append.Op,
            Actor = append.Actor.Value,
            Tenant = append.Tenant.Value,
            At = atUtc,
            Justification = append.Justification,
            PayloadJson = payloadCanonical,
            Signature = append.Signature,
            PrevAuditId = prev?.AuditId,
            Hash = hash,
        };
        db.AuditRecords.Add(row);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new AuditId(row.AuditId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditRecord> QueryAsync(
        AuditQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        IQueryable<AuditRow> q = db.AuditRecords.AsNoTracking();
        if (query.Entity is { } entity)
        {
            q = q.Where(a => a.EntityScheme == entity.Scheme &&
                             a.EntityAuthority == entity.Authority &&
                             a.EntityLocalPart == entity.LocalPart);
        }
        if (query.Actor is { } actor)
            q = q.Where(a => a.Actor == actor.Value);
        if (query.Tenant is { } tenant)
            q = q.Where(a => a.Tenant == tenant.Value);
        if (query.Op is { } op)
            q = q.Where(a => a.Op == (int)op);
        if (query.FromInclusive is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            q = q.Where(a => a.At >= fromUtc);
        }
        if (query.ToExclusive is { } to)
        {
            var toUtc = to.ToUniversalTime();
            q = q.Where(a => a.At < toUtc);
        }

        q = q.OrderBy(a => a.At).ThenBy(a => a.AuditId);

        var rows = await q.ToListAsync(ct).ConfigureAwait(false);
        int emitted = 0;
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            yield return Materialize(row);
            emitted++;
            if (query.Limit is { } limit && emitted >= limit) yield break;
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyChainAsync(EntityId entity, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.AuditRecords.AsNoTracking()
            .Where(a => a.EntityScheme == entity.Scheme &&
                        a.EntityAuthority == entity.Authority &&
                        a.EntityLocalPart == entity.LocalPart)
            .OrderBy(a => a.At)
            .ThenBy(a => a.AuditId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (rows.Count == 0) return true;

        var records = rows.Select(Materialize).ToList();
        return HashChain.Verify(records);
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = 10; // 100ns ticks per µs.
        var extraTicks = value.Ticks % ticksPerMicrosecond;
        return extraTicks == 0 ? value : value.AddTicks(-extraTicks);
    }

    internal static AuditRecord Materialize(AuditRow row)
    {
        var entity = new EntityId(row.EntityScheme, row.EntityAuthority, row.EntityLocalPart);
        VersionId? versionId = (row.VersionSequence, row.VersionHash) switch
        {
            (int seq, string hash) => new VersionId(entity, seq, hash),
            _ => null,
        };
        var payload = JsonDocument.Parse(row.PayloadJson);
        return new AuditRecord(
            Id: new AuditId(row.AuditId),
            EntityId: entity,
            VersionId: versionId,
            Op: (Op)row.Op,
            Actor: new ActorId(row.Actor),
            Tenant: new TenantId(row.Tenant),
            At: row.At,
            Justification: row.Justification,
            Payload: payload,
            Signature: row.Signature,
            Prev: row.PrevAuditId is { } pid ? new AuditId?(new AuditId(pid)) : null,
            Hash: row.Hash);
    }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>
/// Append-only hash-chained audit log.
/// </summary>
/// <remarks>
/// Spec §3.3. The log owns the <see cref="AuditId"/> and <see cref="AuditRecord.Hash"/>;
/// callers supply the operational fields via <see cref="AuditAppend"/>.
/// </remarks>
public interface IAuditLog
{
    /// <summary>Appends a record; returns the allocated <see cref="AuditId"/>.</summary>
    Task<AuditId> AppendAsync(AuditAppend append, CancellationToken ct = default);

    /// <summary>Streams records matching <paramref name="query"/>, ordered by time ascending.</summary>
    IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>
    /// Walks the per-entity chain and returns <c>true</c> iff every hash + prev-link lines up.
    /// </summary>
    Task<bool> VerifyChainAsync(EntityId entity, CancellationToken ct = default);
}

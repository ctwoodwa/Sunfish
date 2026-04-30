using System.Collections.Concurrent;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="IConsentRegistry"/> for tests + non-production
/// hosts. Persists every record in a thread-safe dictionary; no event
/// log, no replication. Phase 1 implementation.
/// </summary>
public sealed class InMemoryConsentRegistry : IConsentRegistry
{
    private readonly ConcurrentDictionary<ConsentRecordId, ConsentRecord> _byId = new();

    /// <inheritdoc />
    public Task<ConsentRecord> RecordAsync(ConsentRecord consent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consent);
        ct.ThrowIfCancellationRequested();
        _byId[consent.Id] = consent;
        return Task.FromResult(consent);
    }

    /// <inheritdoc />
    public Task<ConsentRecord?> GetCurrentAsync(TenantId tenant, ActorId principal, DateTimeOffset asOf, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var current = _byId.Values
            .Where(c => c.Tenant == tenant && c.Principal == principal && c.IsCurrentAt(asOf))
            .OrderByDescending(c => c.GivenAt)
            .FirstOrDefault();
        return Task.FromResult<ConsentRecord?>(current);
    }

    /// <inheritdoc />
    public Task RevokeAsync(ConsentRecordId id, DateTimeOffset revokedAt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_byId.TryGetValue(id, out var record))
        {
            _byId[id] = record with { RevokedAt = revokedAt };
        }
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Audit;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="IConsentRegistry"/> for tests + non-production
/// hosts. Persists every record in a thread-safe dictionary; emits
/// <see cref="AuditEventType.ConsentRecorded"/> +
/// <see cref="AuditEventType.ConsentRevoked"/> when wired with a
/// <see cref="SignatureAuditEmitter"/>.
/// </summary>
public sealed class InMemoryConsentRegistry : IConsentRegistry
{
    private readonly ConcurrentDictionary<ConsentRecordId, ConsentRecord> _byId = new();
    private readonly SignatureAuditEmitter? _audit;
    private readonly TimeProvider _time;

    /// <summary>Creates the registry with audit emission disabled.</summary>
    public InMemoryConsentRegistry() : this(audit: null, time: null) { }

    /// <summary>Creates the registry with optional audit emission + clock.</summary>
    public InMemoryConsentRegistry(SignatureAuditEmitter? audit, TimeProvider? time)
    {
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ConsentRecord> RecordAsync(ConsentRecord consent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consent);
        ct.ThrowIfCancellationRequested();
        _byId[consent.Id] = consent;
        if (_audit is not null)
        {
            await _audit.EmitAsync(
                AuditEventType.ConsentRecorded,
                SignatureAuditPayloadFactory.ConsentRecorded(consent),
                _time.GetUtcNow(),
                ct).ConfigureAwait(false);
        }
        return consent;
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
    public async Task RevokeAsync(ConsentRecordId id, DateTimeOffset revokedAt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_byId.TryGetValue(id, out var record))
        {
            _byId[id] = record with { RevokedAt = revokedAt };
            if (_audit is not null)
            {
                await _audit.EmitAsync(
                    AuditEventType.ConsentRevoked,
                    SignatureAuditPayloadFactory.ConsentRevoked(id, revokedAt),
                    _time.GetUtcNow(),
                    ct).ConfigureAwait(false);
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Audit;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="ISignatureRevocationLog"/> for tests + non-production
/// hosts. Append-only log delegating the validity projection to
/// <see cref="RevocationProjection.Project"/> per ADR 0054 amendments
/// A4 + A5 (last-revocation-wins; ties broken by total-order on Guid).
/// Emits <see cref="AuditEventType.SignatureRevoked"/> +
/// <see cref="AuditEventType.SignatureValidityProjected"/> when wired
/// with a <see cref="SignatureAuditEmitter"/>.
/// </summary>
public sealed class InMemorySignatureRevocationLog : ISignatureRevocationLog
{
    private readonly ConcurrentDictionary<SignatureEventId, List<SignatureRevocation>> _bySignature = new();
    private readonly ConcurrentDictionary<RevocationEventId, byte> _seenIds = new();
    private readonly SignatureAuditEmitter? _audit;
    private readonly TimeProvider _time;

    /// <summary>Creates the log with audit emission disabled.</summary>
    public InMemorySignatureRevocationLog() : this(audit: null, time: null) { }

    /// <summary>Creates the log with optional audit emission + clock.</summary>
    public InMemorySignatureRevocationLog(SignatureAuditEmitter? audit, TimeProvider? time)
    {
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task AppendAsync(SignatureRevocation revocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        ct.ThrowIfCancellationRequested();

        // Idempotent on repeated submission of the same revocation event id.
        if (!_seenIds.TryAdd(revocation.Id, 0))
        {
            return;
        }

        var bucket = _bySignature.GetOrAdd(revocation.SignatureEvent, _ => new List<SignatureRevocation>());
        lock (bucket)
        {
            bucket.Add(revocation);
        }

        if (_audit is not null)
        {
            await _audit.EmitAsync(
                AuditEventType.SignatureRevoked,
                SignatureAuditPayloadFactory.SignatureRevoked(revocation),
                revocation.RevokedAt,
                ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<SignatureValidityStatus> GetCurrentValidityAsync(SignatureEventId signatureId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        SignatureValidityStatus status;
        if (!_bySignature.TryGetValue(signatureId, out var bucket))
        {
            status = new SignatureValidityStatus { IsValid = true };
        }
        else
        {
            SignatureRevocation[] snapshot;
            lock (bucket) { snapshot = bucket.ToArray(); }
            status = RevocationProjection.Project(snapshot);
        }

        if (_audit is not null)
        {
            await _audit.EmitAsync(
                AuditEventType.SignatureValidityProjected,
                SignatureAuditPayloadFactory.SignatureValidityProjected(signatureId, status),
                _time.GetUtcNow(),
                ct).ConfigureAwait(false);
        }
        return status;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SignatureRevocation> ListRevocationsAsync(SignatureEventId signatureId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (_bySignature.TryGetValue(signatureId, out var bucket))
        {
            SignatureRevocation[] snapshot;
            lock (bucket) { snapshot = bucket.ToArray(); }
            foreach (var r in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                yield return r;
                await Task.Yield();
            }
        }
    }
}

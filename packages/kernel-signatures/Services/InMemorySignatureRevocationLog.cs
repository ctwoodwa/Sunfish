using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="ISignatureRevocationLog"/> for tests + non-production
/// hosts. Phase 1 ships an append-only log + a simple "any revocation
/// invalidates" projection; the full last-revocation-wins merge rule
/// per ADR 0054 amendments A4 + A5 lands in W#21 Phase 3.
/// </summary>
public sealed class InMemorySignatureRevocationLog : ISignatureRevocationLog
{
    private readonly ConcurrentDictionary<SignatureEventId, List<SignatureRevocation>> _bySignature = new();
    private readonly ConcurrentDictionary<RevocationEventId, byte> _seenIds = new();

    /// <inheritdoc />
    public Task AppendAsync(SignatureRevocation revocation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        ct.ThrowIfCancellationRequested();

        // Idempotent on repeated submission of the same revocation event id.
        if (!_seenIds.TryAdd(revocation.Id, 0))
        {
            return Task.CompletedTask;
        }

        var bucket = _bySignature.GetOrAdd(revocation.SignatureEvent, _ => new List<SignatureRevocation>());
        lock (bucket)
        {
            bucket.Add(revocation);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SignatureValidityStatus> GetCurrentValidityAsync(SignatureEventId signatureId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_bySignature.TryGetValue(signatureId, out var bucket))
        {
            return Task.FromResult(new SignatureValidityStatus { IsValid = true });
        }

        SignatureRevocation? winning;
        lock (bucket)
        {
            // Phase 1 simplification: any revocation invalidates. Phase 3 will
            // implement the full last-revocation-wins merge with total-order
            // tie-break per ADR 0054 amendments A4 + A5.
            winning = bucket
                .OrderByDescending(r => r.RevokedAt)
                .ThenByDescending(r => r.Id.Value)
                .FirstOrDefault();
        }

        return Task.FromResult(new SignatureValidityStatus
        {
            IsValid = winning is null,
            RevokedBy = winning,
        });
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

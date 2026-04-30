using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="ISignatureRevocationLog"/> for tests + non-production
/// hosts. Append-only log delegating the validity projection to
/// <see cref="RevocationProjection.Project"/> per ADR 0054 amendments
/// A4 + A5 (last-revocation-wins; ties broken by total-order on Guid).
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

        SignatureRevocation[] snapshot;
        lock (bucket) { snapshot = bucket.ToArray(); }

        return Task.FromResult(RevocationProjection.Project(snapshot));
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

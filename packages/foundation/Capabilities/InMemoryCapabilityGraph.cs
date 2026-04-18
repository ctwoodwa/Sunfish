using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Single-process, in-memory implementation of <see cref="ICapabilityGraph"/>. Suitable
/// for unit tests, single-node development, and scenarios where the process is the trust
/// boundary. Not durable; not clustered.
/// </summary>
/// <remarks>
/// <para>Thread safety: the op log, principal set, and replay-protection set are guarded by
/// a single <c>_sync</c> lock. Signature verification is performed outside the lock.</para>
/// <para>Authority validation (checking whether the issuer is allowed to delegate, add
/// members, etc.) is stubbed in Task 3 and filled in by Task 4.</para>
/// </remarks>
public sealed class InMemoryCapabilityGraph(IOperationVerifier verifier) : ICapabilityGraph
{
    private readonly IOperationVerifier _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    private readonly ConcurrentDictionary<PrincipalId, Principal> _principals = new();
    private readonly List<SignedOperation<CapabilityOp>> _opLog = new();
    private readonly HashSet<(PrincipalId Issuer, Guid Nonce)> _processedNonces = new();
    private readonly object _sync = new();

    /// <inheritdoc />
    public ValueTask<bool> QueryAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        bool result;
        lock (_sync)
        {
            // Snapshot is safe: inside the lock, state is consistent.
            result = CapabilityClosure.HasCapability(_principals, _opLog, subject, resource, action, asOf);
        }
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<MutationResult> MutateAsync(
        SignedOperation<CapabilityOp> op,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(op);
        ct.ThrowIfCancellationRequested();

        // Signature check happens outside the lock — it's pure and potentially CPU-heavy.
        if (!_verifier.Verify(op))
            return ValueTask.FromResult(MutationResult.Rejected("Invalid signature"));

        lock (_sync)
        {
            if (!_processedNonces.Add((op.IssuerId, op.Nonce)))
                return ValueTask.FromResult(MutationResult.Rejected("Duplicate nonce"));

            // Task 4 fills in real authority validation. Task 3 skeleton is permissive.
            var authorityCheck = ValidateAuthority(op);
            if (authorityCheck.Kind == MutationKind.Rejected)
                return ValueTask.FromResult(authorityCheck);

            ApplyOp(op);
            _opLog.Add(op);
        }

        return ValueTask.FromResult(MutationResult.Accepted);
    }

    /// <inheritdoc />
    public ValueTask<CapabilityProof?> ExportProofAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        CapabilityProof? proof;
        lock (_sync)
        {
            var chain = CapabilityClosure.FindProofChain(_principals, _opLog, subject, resource, action, asOf);
            proof = chain is null
                ? null
                : new CapabilityProof(subject, resource, action, chain, asOf);
        }
        return ValueTask.FromResult(proof);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SignedOperation<CapabilityOp>> ListOpsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<SignedOperation<CapabilityOp>> snapshot;
        lock (_sync)
        {
            snapshot = _opLog.ToList();
        }

        foreach (var op in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return op;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Placeholder for Task 4. Returns <see cref="MutationResult.Accepted"/> unconditionally
    /// so the skeleton can be exercised end-to-end. Task 4 will enforce that the issuer has
    /// delegate rights / group-admin rights / mint authority as appropriate.
    /// </summary>
    private static MutationResult ValidateAuthority(SignedOperation<CapabilityOp> op)
    {
        _ = op;
        return MutationResult.Accepted;
    }

    private void ApplyOp(SignedOperation<CapabilityOp> op)
    {
        switch (op.Payload)
        {
            case MintPrincipal mint:
                _principals[mint.NewId] = mint.Kind == PrincipalKind.Individual
                    ? new Individual(mint.NewId)
                    : new Group(mint.NewId, mint.InitialMembers ?? Array.Empty<PrincipalId>());
                break;

            case AddMember am:
                if (_principals.TryGetValue(am.Group, out var addTarget) && addTarget is Group addGroup)
                {
                    var updated = addGroup with
                    {
                        Members = addGroup.Members.Concat(new[] { am.Member }).Distinct().ToList(),
                    };
                    _principals[am.Group] = updated;
                }
                break;

            case RemoveMember rm:
                if (_principals.TryGetValue(rm.Group, out var removeTarget) && removeTarget is Group removeGroup)
                {
                    var updated = removeGroup with
                    {
                        Members = removeGroup.Members.Where(m => !m.Equals(rm.Member)).ToList(),
                    };
                    _principals[rm.Group] = updated;
                }
                break;

            case Delegate:
            case Revoke:
                // No principal-set mutation; the closure reads these from the op log.
                break;
        }
    }
}

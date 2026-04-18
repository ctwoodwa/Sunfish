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
    /// Enforces the Phase B authority model over the current <c>_principals</c> and
    /// <c>_opLog</c> snapshot. Must be called with <c>_sync</c> held; reads the shared state
    /// but does not mutate it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><see cref="MintPrincipal"/>: always accepted (Phase B is open-mint;
    ///     Phase E will gate minting).</description></item>
    ///   <item><description><see cref="Delegate"/>: accepted when bootstrapping the resource
    ///     (no prior Delegate on the resource) OR when the issuer transitively holds the
    ///     <see cref="CapabilityAction.Delegate"/> capability on the resource at
    ///     <c>op.IssuedAt</c>.</description></item>
    ///   <item><description><see cref="Revoke"/>: only the original Delegate issuer may revoke
    ///     their own grant.</description></item>
    ///   <item><description><see cref="AddMember"/> / <see cref="RemoveMember"/>: accepted on
    ///     bootstrap (no prior membership op for the group) OR when the issuer holds
    ///     <see cref="CapabilityAction.Delegate"/> on the synthetic resource
    ///     <c>group:&lt;groupId&gt;</c>.</description></item>
    /// </list>
    /// </remarks>
    private MutationResult ValidateAuthority(SignedOperation<CapabilityOp> op)
    {
        switch (op.Payload)
        {
            case MintPrincipal:
                return MutationResult.Accepted;

            case Delegate d:
                // Bootstrap: the first Delegate on a resource establishes the root owner.
                if (!_opLog.Any(existing => existing.Payload is Delegate ed && ed.Resource == d.Resource))
                    return MutationResult.Accepted;
                if (CapabilityClosure.HasCapability(_principals, _opLog, op.IssuerId, d.Resource, CapabilityAction.Delegate, op.IssuedAt))
                    return MutationResult.Accepted;
                return MutationResult.Rejected("Issuer lacks Delegate capability on resource");

            case Revoke r:
            {
                var originalDelegate = _opLog.LastOrDefault(existing =>
                    existing.Payload is Delegate ed
                    && ed.Subject.Equals(r.Subject)
                    && ed.Resource == r.Resource
                    && ed.Action == r.Action);
                if (originalDelegate is null)
                    return MutationResult.Rejected("No matching delegate to revoke");
                if (!originalDelegate.IssuerId.Equals(op.IssuerId))
                    return MutationResult.Rejected("Only the original issuer may revoke");
                return MutationResult.Accepted;
            }

            case AddMember am:
            {
                var groupResource = new Resource($"group:{am.Group.ToBase64Url()}");
                // Bootstrap: first member-op for the group is allowed (group creator manages it).
                if (!HasPriorMembershipOp(am.Group))
                    return MutationResult.Accepted;
                if (CapabilityClosure.HasCapability(_principals, _opLog, op.IssuerId, groupResource, CapabilityAction.Delegate, op.IssuedAt))
                    return MutationResult.Accepted;
                return MutationResult.Rejected("Issuer lacks Delegate capability on group");
            }

            case RemoveMember rm:
            {
                var groupResource = new Resource($"group:{rm.Group.ToBase64Url()}");
                if (!HasPriorMembershipOp(rm.Group))
                    return MutationResult.Accepted;
                if (CapabilityClosure.HasCapability(_principals, _opLog, op.IssuerId, groupResource, CapabilityAction.Delegate, op.IssuedAt))
                    return MutationResult.Accepted;
                return MutationResult.Rejected("Issuer lacks Delegate capability on group");
            }
        }
        return MutationResult.Rejected("Unknown op kind");
    }

    private bool HasPriorMembershipOp(PrincipalId group)
    {
        foreach (var existing in _opLog)
        {
            if (existing.Payload is AddMember ae && ae.Group.Equals(group)) return true;
            if (existing.Payload is RemoveMember re && re.Group.Equals(group)) return true;
        }
        return false;
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

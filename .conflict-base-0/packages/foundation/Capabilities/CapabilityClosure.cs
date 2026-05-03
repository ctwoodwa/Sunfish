using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Pure functions that compute the transitive closure of the capability graph over its
/// current principal set and op log. Implements the Phase B query algorithm: walks the
/// delegation log, expands group membership, and honors expiration / revocation /
/// as-of time-travel semantics.
/// </summary>
/// <remarks>
/// <para>
/// The closure is computed lazily at query time — the op log is the source of truth;
/// the principal set is a materialized projection of MintPrincipal / AddMember / RemoveMember
/// ops (already maintained by <see cref="InMemoryCapabilityGraph"/>).
/// </para>
/// <para>
/// Cycle safety: <see cref="IsTransitiveMember"/> uses a visited set so self-membership
/// and mutually-recursive groups terminate rather than stack-overflowing. The recursive
/// <see cref="HasCapability"/> call for issuer-authority walks across issuer principals
/// rather than group membership, and is guarded by the finite set of active-delegate
/// entries — pathological chains still terminate but may be O(|delegates|^2) in the worst
/// case; Phase E will replace this with a more rigorous model.
/// </para>
/// </remarks>
internal static class CapabilityClosure
{
    /// <summary>
    /// Decides whether <paramref name="subject"/> holds <paramref name="action"/> on
    /// <paramref name="resource"/> as of <paramref name="asOf"/>.
    /// </summary>
    public static bool HasCapability(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf)
    {
        var activeDelegates = GetActiveDelegates(opLog, resource, action, asOf);
        foreach (var (issuer, target) in activeDelegates)
        {
            if (IsTransitiveMember(principals, target, subject))
            {
                if (IsRootOwner(opLog, issuer, resource) ||
                    HasCapability(principals, opLog, issuer, resource, CapabilityAction.Delegate, asOf))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds the minimal ordered op chain that justifies the capability, or returns
    /// <c>null</c> when the subject does not hold it.
    /// </summary>
    public static IReadOnlyList<SignedOperation<CapabilityOp>>? FindProofChain(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf)
    {
        var activeDelegates = GetActiveDelegates(opLog, resource, action, asOf).ToList();
        foreach (var (issuer, target) in activeDelegates)
        {
            if (IsTransitiveMember(principals, target, subject))
            {
                var proof = new List<SignedOperation<CapabilityOp>>();
                // The Delegate op that grants target -> resource/action.
                var delegateOp = opLog.Last(op => op.Payload is Delegate d
                    && d.Subject.Equals(target) && d.Resource == resource && d.Action == action
                    && op.IssuerId.Equals(issuer));
                proof.Add(delegateOp);
                // The AddMember ops that put subject transitively under target.
                proof.AddRange(TraceMembershipOps(opLog, target, subject));
                return proof;
            }
        }
        return null;
    }

    /// <summary>
    /// Walks the op log and returns the set of (issuer, subject) pairs whose Delegate for the
    /// given (resource, action) is currently active as of <paramref name="asOf"/>.
    /// </summary>
    /// <remarks>
    /// Revoke is applied with <c>op.IssuedAt &lt;= asOf</c> semantics so point-in-time queries
    /// see the historical decision rather than any later revocation. This is a tightening of
    /// the plan-as-written (which would apply Revoke unconditionally) — it matters for
    /// time-travel queries like auditing who had access on a prior date.
    /// </remarks>
    private static IEnumerable<(PrincipalId Issuer, PrincipalId Target)> GetActiveDelegates(
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf)
    {
        var delegates = new Dictionary<(PrincipalId Issuer, PrincipalId Subject), SignedOperation<CapabilityOp>>();
        foreach (var op in opLog)
        {
            switch (op.Payload)
            {
                case Delegate d
                    when d.Resource == resource
                      && d.Action == action
                      && op.IssuedAt <= asOf
                      && (d.Expires is null || d.Expires > asOf):
                    delegates[(op.IssuerId, d.Subject)] = op;
                    break;
                case Revoke r
                    when r.Resource == resource
                      && r.Action == action
                      && op.IssuedAt <= asOf:
                    delegates.Remove((op.IssuerId, r.Subject));
                    break;
            }
        }
        return delegates.Keys;
    }

    /// <summary>
    /// BFS over group membership from <paramref name="container"/> looking for
    /// <paramref name="candidate"/>. Cycle-safe via a visited set.
    /// </summary>
    private static bool IsTransitiveMember(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        PrincipalId container,
        PrincipalId candidate)
    {
        if (container.Equals(candidate)) return true;
        var visited = new HashSet<PrincipalId> { container };
        var queue = new Queue<PrincipalId>();
        queue.Enqueue(container);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!principals.TryGetValue(current, out var p) || p is not Group g) continue;
            foreach (var m in g.Members)
            {
                if (m.Equals(candidate)) return true;
                if (visited.Add(m)) queue.Enqueue(m);
            }
        }
        return false;
    }

    /// <summary>
    /// Phase B root-owner heuristic: the issuer of the first Delegate on a resource is the
    /// root owner. Phase E will replace this with an explicit resource-mint model.
    /// </summary>
    private static bool IsRootOwner(
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId candidate,
        Resource resource)
    {
        var firstDelegate = opLog.FirstOrDefault(op => op.Payload is Delegate d && d.Resource == resource);
        return firstDelegate is not null && firstDelegate.IssuerId.Equals(candidate);
    }

    /// <summary>
    /// Reconstructs the AddMember ops that link <paramref name="from"/> transitively down to
    /// <paramref name="to"/>. Returns the ops in top-down order (closest to <paramref name="from"/> first).
    /// Note: this is a structural proof built purely from AddMember records; it does not
    /// account for RemoveMember ops (materialization of group state handles that).
    /// </summary>
    private static IEnumerable<SignedOperation<CapabilityOp>> TraceMembershipOps(
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId from,
        PrincipalId to)
    {
        if (from.Equals(to)) yield break;

        // Build a child -> [(parent-group, AddMember op)] map.
        var addEdges = new Dictionary<PrincipalId, List<(PrincipalId Parent, SignedOperation<CapabilityOp> Op)>>();
        foreach (var op in opLog)
        {
            if (op.Payload is AddMember am)
            {
                if (!addEdges.TryGetValue(am.Member, out var list))
                    addEdges[am.Member] = list = new();
                list.Add((am.Group, op));
            }
        }

        // BFS from 'to' upward toward 'from' via the parent map; record predecessors so we can
        // reconstruct the chain walking back down.
        var predecessor = new Dictionary<PrincipalId, (PrincipalId Parent, SignedOperation<CapabilityOp> Op)>();
        var visited = new HashSet<PrincipalId> { to };
        var queue = new Queue<PrincipalId>();
        queue.Enqueue(to);
        bool found = false;
        while (queue.Count > 0 && !found)
        {
            var current = queue.Dequeue();
            if (!addEdges.TryGetValue(current, out var parents)) continue;
            foreach (var (parent, addOp) in parents)
            {
                if (!visited.Add(parent)) continue;
                predecessor[parent] = (current, addOp);
                if (parent.Equals(from)) { found = true; break; }
                queue.Enqueue(parent);
            }
        }
        if (!found) yield break;

        // Walk predecessor from 'from' back down to 'to', yielding AddMember ops.
        var cursor = from;
        while (!cursor.Equals(to) && predecessor.TryGetValue(cursor, out var step))
        {
            yield return step.Op;
            cursor = step.Parent;
        }
    }
}

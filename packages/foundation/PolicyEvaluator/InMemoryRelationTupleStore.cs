using System.Runtime.CompilerServices;

namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// An in-memory, thread-safe implementation of <see cref="IRelationTupleStore"/> backed by a
/// <see cref="HashSet{T}"/> under a single lock.
/// </summary>
/// <remarks>
/// Suitable for tests, Phase B demos, and small embedded deployments. Record equality on
/// <see cref="UsersetRef"/> and <see cref="PolicyResource"/> makes tuples compare structurally
/// without any extra plumbing.
/// </remarks>
public sealed class InMemoryRelationTupleStore : IRelationTupleStore
{
    private readonly object _gate = new();
    private readonly HashSet<(UsersetRef User, string Relation, PolicyResource Object)> _tuples = new();

    /// <inheritdoc />
    public ValueTask AddAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(@object);
        lock (_gate)
        {
            _tuples.Add((user, relation, @object));
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(@object);
        lock (_gate)
        {
            _tuples.Remove((user, relation, @object));
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(@object);
        bool exists;
        lock (_gate)
        {
            exists = _tuples.Contains((user, relation, @object));
        }
        return ValueTask.FromResult(exists);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<UsersetRef> ListUsersAsync(
        string relation,
        PolicyResource @object,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(@object);

        // Snapshot under the lock so enumeration is stable.
        UsersetRef[] snapshot;
        lock (_gate)
        {
            snapshot = _tuples
                .Where(t => t.Relation == relation && t.Object.Equals(@object))
                .Select(t => t.User)
                .ToArray();
        }

        foreach (var u in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return u;
        }

        await ValueTask.CompletedTask;
    }
}

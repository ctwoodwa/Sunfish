using System.Collections.Concurrent;
using Sunfish.Federation.EntitySync.Protocol;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync;

/// <summary>
/// Volatile in-process <see cref="IChangeStore"/>. Suitable for tests and single-node dev; replace
/// with a durable implementation for production federation nodes.
/// </summary>
/// <remarks>
/// The backing <see cref="ConcurrentDictionary{TKey, TValue}"/> provides concurrent Put/Contains;
/// <see cref="GetHeads"/> and <see cref="GetReachableFrom"/> take a monitor lock for a consistent
/// snapshot over the dictionary (iterations and parent-set computations must see a single state).
/// </remarks>
public sealed class InMemoryChangeStore : IChangeStore
{
    private readonly ConcurrentDictionary<VersionId, SignedOperation<ChangeRecord>> _changes = new();
    private readonly object _sync = new();

    /// <inheritdoc />
    public bool Contains(VersionId version) => _changes.ContainsKey(version);

    /// <inheritdoc />
    public SignedOperation<ChangeRecord>? TryGet(VersionId version)
        => _changes.TryGetValue(version, out var v) ? v : null;

    /// <inheritdoc />
    public void Put(SignedOperation<ChangeRecord> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _changes[change.Payload.VersionId] = change;
    }

    /// <inheritdoc />
    public IReadOnlyList<VersionId> GetHeads(EntityId? scope)
    {
        lock (_sync)
        {
            var all = _changes.Values
                .Where(c => scope is null || c.Payload.EntityId.Equals(scope.Value))
                .ToList();

            var parents = new HashSet<VersionId>();
            foreach (var c in all)
            {
                if (c.Payload.ParentVersionId is { } parent)
                    parents.Add(parent);
            }

            var heads = new List<VersionId>();
            foreach (var c in all)
            {
                if (!parents.Contains(c.Payload.VersionId))
                    heads.Add(c.Payload.VersionId);
            }
            return heads;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SignedOperation<ChangeRecord>> GetReachableFrom(
        IReadOnlyList<VersionId> heads,
        IReadOnlyCollection<VersionId> stopAt)
    {
        ArgumentNullException.ThrowIfNull(heads);
        ArgumentNullException.ThrowIfNull(stopAt);

        lock (_sync)
        {
            var result = new List<SignedOperation<ChangeRecord>>();
            var visited = new HashSet<VersionId>();
            var stopSet = new HashSet<VersionId>(stopAt);
            var queue = new Queue<VersionId>();
            foreach (var h in heads)
            {
                if (!stopSet.Contains(h))
                    queue.Enqueue(h);
            }

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!visited.Add(v)) continue;
                if (!_changes.TryGetValue(v, out var change)) continue;
                result.Add(change);
                if (change.Payload.ParentVersionId is { } parent && !stopSet.Contains(parent))
                    queue.Enqueue(parent);
            }
            return result;
        }
    }
}

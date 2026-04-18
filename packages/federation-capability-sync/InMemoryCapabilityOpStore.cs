using System.Collections.Concurrent;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.CapabilitySync;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ICapabilityOpStore"/> backed by a
/// concurrent dictionary keyed on the op nonce.
/// </summary>
public sealed class InMemoryCapabilityOpStore : ICapabilityOpStore
{
    private readonly ConcurrentDictionary<Guid, SignedOperation<CapabilityOp>> _ops = new();

    /// <inheritdoc />
    public bool Contains(Guid nonce) => _ops.ContainsKey(nonce);

    /// <inheritdoc />
    public SignedOperation<CapabilityOp>? TryGet(Guid nonce)
        => _ops.TryGetValue(nonce, out var op) ? op : null;

    /// <inheritdoc />
    public void Put(SignedOperation<CapabilityOp> op)
    {
        ArgumentNullException.ThrowIfNull(op);
        _ops[op.Nonce] = op;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> AllNonces() => _ops.Keys.ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<SignedOperation<CapabilityOp>> All() => _ops.Values.ToArray();
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Reference <see cref="IVersionVectorExchange"/> wired around a configured
/// <see cref="ICompatibilityRelation"/>. Stateless + thread-safe.
/// </summary>
public sealed class InMemoryVersionVectorExchange : IVersionVectorExchange
{
    private readonly ICompatibilityRelation _relation;

    /// <summary>Constructs the exchange around <paramref name="relation"/>.</summary>
    public InMemoryVersionVectorExchange(ICompatibilityRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        _relation = relation;
    }

    /// <inheritdoc />
    public ValueTask<VersionVectorVerdict> EvaluateAsync(
        VersionVector localVector,
        VersionVector peerVector,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(localVector);
        ArgumentNullException.ThrowIfNull(peerVector);
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_relation.Evaluate(localVector, peerVector));
    }
}

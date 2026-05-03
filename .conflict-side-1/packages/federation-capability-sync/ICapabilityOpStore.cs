using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.CapabilitySync;

/// <summary>
/// Flat nonce-indexed store of signed capability operations. This is a sync-layer concern only —
/// downstream application of these ops into an <see cref="ICapabilityGraph"/> is a separate
/// responsibility outside this package.
/// </summary>
public interface ICapabilityOpStore
{
    /// <summary>Returns whether an op with the given nonce is present in the store.</summary>
    bool Contains(Guid nonce);

    /// <summary>Returns the op with the given nonce, or <c>null</c> if not present.</summary>
    SignedOperation<CapabilityOp>? TryGet(Guid nonce);

    /// <summary>Inserts or replaces the op keyed by its <see cref="SignedOperation{T}.Nonce"/>.</summary>
    void Put(SignedOperation<CapabilityOp> op);

    /// <summary>Returns a snapshot of every nonce currently stored.</summary>
    IReadOnlyCollection<Guid> AllNonces();

    /// <summary>Returns a snapshot of every op currently stored.</summary>
    IReadOnlyCollection<SignedOperation<CapabilityOp>> All();
}

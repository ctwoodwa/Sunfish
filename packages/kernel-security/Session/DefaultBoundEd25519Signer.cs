using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Session;

/// <summary>
/// Default <see cref="IBoundEd25519Signer"/> implementation. Holds the
/// private-key seed in an internal field; <see cref="SignAsync"/> calls
/// <see cref="IEd25519Signer.Sign"/> with it. The seed is copied
/// defensively on construction so the caller cannot mutate it post-hoc.
/// </summary>
public sealed class DefaultBoundEd25519Signer : IBoundEd25519Signer
{
    private readonly IEd25519Signer _signer;
    private readonly byte[] _privateKeySeed;

    public DefaultBoundEd25519Signer(
        IEd25519Signer signer,
        ReadOnlySpan<byte> privateKeySeed,
        ReadOnlySpan<byte> publicKey)
    {
        ArgumentNullException.ThrowIfNull(signer);
        _signer = signer;
        _privateKeySeed = privateKeySeed.ToArray();
        PublicKey = publicKey.ToArray();
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> PublicKey { get; }

    /// <inheritdoc />
    public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sig = _signer.Sign(data.Span, _privateKeySeed);
        return ValueTask.FromResult(sig);
    }
}

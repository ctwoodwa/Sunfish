namespace Sunfish.Kernel.Security.Session;

/// <summary>
/// An Ed25519 signer pre-loaded with a session identity private key.
/// The private key is never exposed to callers — only the public key
/// and the <see cref="SignAsync"/> primitive are visible.
///
/// W#65 — answer to cob-question 2026-05-16T04-42Z. Consumed by
/// W#63 <c>ApproveRecoveryPage</c> to sign trustee attestations without
/// exposing the trustee's identity private-key bytes to the Razor page.
/// </summary>
public interface IBoundEd25519Signer
{
    /// <summary>The Ed25519 public key (32 bytes) matching the held private key.</summary>
    ReadOnlyMemory<byte> PublicKey { get; }

    /// <summary>
    /// Signs <paramref name="data"/> with the held private key. Returns a
    /// 64-byte Ed25519 signature.
    /// </summary>
    ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

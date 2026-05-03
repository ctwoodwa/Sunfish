namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Helpers that mint fresh Ed25519 identities + <see cref="LocalIdentity"/>
/// instances for tests. Keeps the existing test suite self-contained and
/// the new sign/verify path exercisable without hand-assembling keypairs in
/// every case.
/// </summary>
internal static class TestIdentityFactory
{
    public static IEd25519Signer NewSigner() => new Ed25519Signer();

    public static NodeIdentity NewNodeIdentity(IEd25519Signer? signer = null)
    {
        signer ??= NewSigner();
        var (publicKey, privateKey) = signer.GenerateKeyPair();
        // Hex of the first 16 bytes of the public key is a stable, debuggable
        // 16-byte node id for tests.
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(publicKey, 0, nodeIdBytes, 0, 16);
        var hex = Convert.ToHexString(nodeIdBytes).ToLowerInvariant();
        return new NodeIdentity(hex, publicKey, privateKey);
    }

    public static LocalIdentity NewLocalIdentity(
        IEd25519Signer? signer = null,
        NodeIdentity? identity = null)
    {
        signer ??= NewSigner();
        identity ??= NewNodeIdentity(signer);
        return new LocalIdentity(
            NodeId: identity.NodeIdBytes,
            PublicKey: identity.PublicKey,
            Signer: signer,
            PrivateKey: identity.PrivateKey,
            SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);
    }
}

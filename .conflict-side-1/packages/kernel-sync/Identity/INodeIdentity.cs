namespace Sunfish.Kernel.Sync.Identity;

/// <summary>
/// Represents the local node's Ed25519 identity — the keypair used to sign HELLO
/// + GOSSIP_PING on the sync-daemon wire.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NodeId"/> is the hex-encoded 16-byte UUID form of the node
/// identifier (matches <see cref="Gossip.VectorClock"/>'s key form and the
/// on-the-wire <c>node_id</c> bstr). The helper <see cref="NodeIdBytes"/>
/// decodes it to the raw 16-byte form that HELLO carries in <c>node_id</c>.
/// </para>
/// <para>
/// <see cref="PrivateKey"/> is the raw 32-byte Ed25519 seed as produced by
/// <c>Sunfish.Kernel.Security.Crypto.IEd25519Signer.GenerateKeyPair</c>. It
/// is <b>secret</b>; never log it. <see cref="PublicKey"/> is the matching
/// 32-byte raw-public-key. Production deployments load these from an
/// <c>IKeystore</c> at boot; the in-memory provider is for tests and the
/// single-node CLI harness.
/// </para>
/// </remarks>
public sealed record NodeIdentity(string NodeId, byte[] PublicKey, byte[] PrivateKey)
{
    /// <summary>
    /// Raw 16-byte wire form of <see cref="NodeId"/>. Decoded from the hex
    /// string lazily on access.
    /// </summary>
    public byte[] NodeIdBytes =>
        string.IsNullOrEmpty(NodeId) ? Array.Empty<byte>() : Convert.FromHexString(NodeId);
}

/// <summary>
/// Provides the local node's Ed25519 identity to handshake and gossip code paths.
/// Implementations typically hold a single immutable <see cref="NodeIdentity"/> per
/// process; rotation is a separate concern that tears down and rebuilds the
/// gossip daemon.
/// </summary>
public interface INodeIdentityProvider
{
    /// <summary>
    /// The local node's identity. Typically loaded from an <c>IKeystore</c> on
    /// first boot by the composition root (e.g. <c>local-node-host</c>). The
    /// in-memory fallback in <see cref="InMemoryNodeIdentityProvider"/> is for
    /// tests and bootstrap; production registers its own implementation.
    /// </summary>
    NodeIdentity Current { get; }
}

/// <summary>
/// Simple <see cref="INodeIdentityProvider"/> that returns a pre-built
/// <see cref="NodeIdentity"/>. Tests and the scaffolding-CLI use this when
/// keystore integration is not required.
/// </summary>
public sealed class InMemoryNodeIdentityProvider : INodeIdentityProvider
{
    public InMemoryNodeIdentityProvider(NodeIdentity identity)
    {
        Current = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    public NodeIdentity Current { get; }
}

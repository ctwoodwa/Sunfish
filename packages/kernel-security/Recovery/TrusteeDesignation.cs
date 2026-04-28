namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// A trustee designated by the owner per sub-pattern <b>#48a</b> of ADR 0046.
/// The designation captures the trustee's stable Sunfish NodeIdentity
/// (the same Ed25519 keypair that signs gossip-protocol HELLO frames) so
/// later <see cref="TrusteeAttestation"/>s can be verified and matched
/// against the designated set.
/// </summary>
/// <remarks>
/// An attestation from a node not present in the designated set is
/// silently dropped by the coordinator — the trust model is "only
/// previously-designated trustees can attest."
/// </remarks>
public sealed record TrusteeDesignation(
    string NodeId,
    byte[] PublicKey,
    DateTimeOffset DesignatedAt);

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// A trustee designated by the owner per sub-pattern <b>#48a</b> of ADR 0046.
/// The designation captures the trustee's stable Sunfish NodeIdentity
/// (the same Ed25519 keypair that signs gossip-protocol HELLO frames) so
/// later <see cref="TrusteeAttestation"/>s can be verified and matched
/// against the designated set, AND the trustee's per-team X25519 public
/// key (W#67 / ADR 0046-A6) so trustee-submitted attestation envelopes
/// can be cross-checked against the originally-recorded DH identity.
/// </summary>
/// <remarks>
/// An attestation from a node not present in the designated set is
/// silently dropped by the coordinator — the trust model is "only
/// previously-designated trustees can attest." Additionally (W#67 PR 5
/// security-engineering council MAJOR-2 binding), an attestation whose
/// <see cref="TrusteeAttestation.TrusteeDHPublicKey"/> does not
/// <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>
/// the designation's <see cref="DHPublicKey"/> is also dropped — preventing
/// a compromised trustee (or attacker who learned a trustee's NodeId +
/// Ed25519 pubkey) from substituting an attacker-controlled DH key whose
/// private half they hold.
/// </remarks>
public sealed record TrusteeDesignation(
    string NodeId,
    byte[] PublicKey,
    byte[] DHPublicKey,
    DateTimeOffset DesignatedAt)
{
    /// <summary>Length in bytes of the trustee's per-team X25519 public key (W#67).</summary>
    public const int DHPublicKeyLength = 32;
}

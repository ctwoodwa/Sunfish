namespace Sunfish.Foundation.Recovery;

/// <summary>
/// W#67 / ADR 0046-A6 — the trustee-side persisted record of a root-seed
/// envelope encrypted FOR a specific trustee BY the owner at trustee-
/// designation time. Set up via
/// <see cref="IRecoveryCoordinator.SetupTrusteeAsync"/>; consumed during
/// the recovery flow by <c>TrusteeSetupPage</c> + <c>ApproveRecoveryPage</c>
/// (W#67 PR 5) to derive the per-attestation re-encrypted envelope.
/// </summary>
/// <param name="TrusteeNodeId">The trustee's durable Sunfish NodeId.</param>
/// <param name="OwnerEphX25519PublicKey">
/// The owner's ephemeral X25519 public key used as the sender during
/// the original <c>IX25519KeyAgreement.Box</c>. The trustee needs this
/// to <c>OpenBox</c> the envelope and recover the root seed.
/// </param>
/// <param name="Ciphertext">
/// The seed envelope ciphertext (32-byte seed + 16-byte auth tag = 48 B).
/// </param>
/// <param name="Nonce">
/// The 24-byte nonce returned by <see cref="Sunfish.Kernel.Security.Crypto.IX25519KeyAgreement.Box"/>.
/// </param>
public sealed record TrusteeEncryptedSeed(
    string TrusteeNodeId,
    byte[] OwnerEphX25519PublicKey,
    byte[] Ciphertext,
    byte[] Nonce);

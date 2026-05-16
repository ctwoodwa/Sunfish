using System.Security.Cryptography;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#67 PR 5 — extracts the trustee-designation + seed-envelope flow
/// from <c>TrusteeSetupPage.razor</c> into a MAUI-free service. The
/// owner runs this for each trustee:
///
///   1. Designate the trustee with their (NodeId, Ed25519 pub, X25519 DH pub).
///   2. Box the install root seed against the trustee's X25519 DH pub
///      via <see cref="IX25519KeyAgreement.Box"/> using a fresh
///      owner-ephemeral X25519 keypair.
///   3. Persist the resulting <see cref="TrusteeEncryptedSeed"/> via
///      <see cref="IRecoveryCoordinator.SetupTrusteeAsync"/>.
///
/// The trustee's local Anchor reads its own
/// <see cref="TrusteeEncryptedSeed"/> entry during the approval flow
/// to re-encrypt the seed toward the recovering device's ephemeral
/// X25519 public key.
/// </summary>
public sealed class TrusteeSetupService
{
    private readonly IRecoveryCoordinator _recovery;
    private readonly IRootSeedProvider _rootSeed;
    private readonly IX25519KeyAgreement _keyAgreement;

    public TrusteeSetupService(
        IRecoveryCoordinator recovery,
        IRootSeedProvider rootSeed,
        IX25519KeyAgreement keyAgreement)
    {
        _recovery     = recovery     ?? throw new ArgumentNullException(nameof(recovery));
        _rootSeed     = rootSeed     ?? throw new ArgumentNullException(nameof(rootSeed));
        _keyAgreement = keyAgreement ?? throw new ArgumentNullException(nameof(keyAgreement));
    }

    /// <summary>
    /// Designate <paramref name="trusteeNodeId"/> as a trustee and
    /// distribute the root-seed envelope to them. Returns the trustee
    /// DH public-key SHA-256 fingerprint (first 8 hex chars) for the
    /// owner to confirm with the trustee verbally.
    /// </summary>
    public async Task<TrusteeSetupOutcome> DesignateAndDistributeAsync(
        string trusteeNodeId,
        byte[] trusteeEd25519PublicKey,
        byte[] trusteeDHPublicKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);
        ArgumentNullException.ThrowIfNull(trusteeEd25519PublicKey);
        ArgumentNullException.ThrowIfNull(trusteeDHPublicKey);
        if (trusteeDHPublicKey.Length != TrusteeDesignation.DHPublicKeyLength)
        {
            throw new ArgumentException(
                $"Trustee DH public key must be {TrusteeDesignation.DHPublicKeyLength} bytes.",
                nameof(trusteeDHPublicKey));
        }

        // 1) Designate. Coordinator's FixedTimeEquals binding (W#67 PR 5
        //    MAJOR-2) requires the DH key here to match every later
        //    attestation's TrusteeDHPublicKey.
        await _recovery.DesignateTrusteeAsync(
            trusteeNodeId, trusteeEd25519PublicKey, trusteeDHPublicKey, cancellationToken)
            .ConfigureAwait(false);

        // 2) Read root seed + Box it for the trustee. Use a FRESH
        //    owner-ephemeral X25519 keypair so the same seed encrypted
        //    to multiple trustees does not reveal correlation via shared
        //    sender-key.
        var rootSeed = await _rootSeed.GetRootSeedAsync(cancellationToken).ConfigureAwait(false);
        var (ownerEphPub, ownerEphPriv) = _keyAgreement.GenerateKeyPair();
        try
        {
            var (ciphertext, nonce) = _keyAgreement.Box(
                plaintext:            rootSeed.Span,
                recipientPublicKey:   trusteeDHPublicKey,
                senderPrivateKey:     ownerEphPriv);

            var envelope = new TrusteeEncryptedSeed(
                TrusteeNodeId:           trusteeNodeId,
                OwnerEphX25519PublicKey: ownerEphPub,
                Ciphertext:              ciphertext,
                Nonce:                   nonce);

            // 3) Persist to coordinator state. SetupTrusteeAsync is
            //    idempotent — re-running for the same trustee overwrites
            //    the envelope (used when the owner re-designates after a
            //    trustee rotates their DH key).
            await _recovery.SetupTrusteeAsync(trusteeNodeId, envelope, cancellationToken)
                .ConfigureAwait(false);

            // Council MAJOR-3: surface BOTH fingerprints so the owner
            // can verbally cross-check with the trustee. Ed25519 vs
            // X25519 paste-swap (both 32 bytes) would otherwise be
            // invisible until the trustee's attestation is silently
            // dropped at recovery time.
            var dhFingerprint = Convert.ToHexString(
                SHA256.HashData(trusteeDHPublicKey))[..16];
            var edFingerprint = Convert.ToHexString(
                SHA256.HashData(trusteeEd25519PublicKey))[..16];
            return new TrusteeSetupOutcome(
                TrusteeNodeId:                 trusteeNodeId,
                TrusteeDHFingerprintHex:       dhFingerprint,
                TrusteeEd25519FingerprintHex:  edFingerprint);
        }
        finally
        {
            // Zero owner-ephemeral private key promptly — it never
            // needs to be persisted; each designation uses a fresh one.
            CryptographicOperations.ZeroMemory(ownerEphPriv);
        }
    }
}

/// <summary>
/// Outcome of <see cref="TrusteeSetupService.DesignateAndDistributeAsync"/>
/// — the trustee NodeId and the 16-char SHA-256 fingerprints of BOTH
/// their Ed25519 (signing) and X25519 (DH) public keys, for verbal
/// cross-confirmation with the trustee (detects paste-swap between the
/// two same-length 32-byte fields).
/// </summary>
public sealed record TrusteeSetupOutcome(
    string TrusteeNodeId,
    string TrusteeDHFingerprintHex,
    string TrusteeEd25519FingerprintHex);

using System.Linq;
using System.Security.Cryptography;
using NSec.Cryptography;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Security.Session;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#66 + W#67 PR 5 — builds and submits a <see cref="TrusteeAttestation"/>
/// for the active pending recovery request. Composition root:
///
///   - <see cref="ISessionSignerAccessor"/> (W#65) — signs the attestation
///     with the trustee's per-team Ed25519 subkey.
///   - <see cref="INodeIdentityProvider"/> — trustee NodeId.
///   - <see cref="IRecoveryCoordinator"/> — reads the trustee's own
///     <see cref="TrusteeEncryptedSeed"/> envelope; submits the
///     attestation.
///   - <see cref="IX25519SubkeyDerivation"/> + <see cref="IRootSeedProvider"/>
///     + <see cref="IActiveTeamAccessor"/> — derive the trustee's per-team
///     X25519 keypair (matches what the owner recorded in
///     <see cref="TrusteeDesignation.DHPublicKey"/>).
///   - <see cref="IX25519KeyAgreement"/> — OpenBox the owner-delivered
///     envelope; re-Box toward the recovering device's
///     <see cref="RecoveryRequest.EphemeralDHPublicKey"/>.
///
/// W#67 PR 5: the attestation now carries the FULL re-encrypted seed
/// envelope keyed to the recovering device. The trustee's per-team
/// X25519 public key (derived locally, matches the designation) goes
/// into <see cref="TrusteeAttestation.TrusteeDHPublicKey"/>.
/// </summary>
public sealed class RecoveryAttestationSubmitter
{
    private readonly IRecoveryCoordinator _recovery;
    private readonly ISessionSignerAccessor _signerAccessor;
    private readonly INodeIdentityProvider _nodeIdentity;
    private readonly IX25519SubkeyDerivation _x25519SubkeyDerivation;
    private readonly IRootSeedProvider _rootSeedProvider;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IX25519KeyAgreement _keyAgreement;
    private readonly TimeProvider _time;

    public RecoveryAttestationSubmitter(
        IRecoveryCoordinator recovery,
        ISessionSignerAccessor signerAccessor,
        INodeIdentityProvider nodeIdentity,
        IX25519SubkeyDerivation x25519SubkeyDerivation,
        IRootSeedProvider rootSeedProvider,
        IActiveTeamAccessor activeTeam,
        IX25519KeyAgreement keyAgreement,
        TimeProvider time)
    {
        _recovery                = recovery                ?? throw new ArgumentNullException(nameof(recovery));
        _signerAccessor          = signerAccessor          ?? throw new ArgumentNullException(nameof(signerAccessor));
        _nodeIdentity            = nodeIdentity            ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _x25519SubkeyDerivation  = x25519SubkeyDerivation  ?? throw new ArgumentNullException(nameof(x25519SubkeyDerivation));
        _rootSeedProvider        = rootSeedProvider        ?? throw new ArgumentNullException(nameof(rootSeedProvider));
        _activeTeam              = activeTeam              ?? throw new ArgumentNullException(nameof(activeTeam));
        _keyAgreement            = keyAgreement            ?? throw new ArgumentNullException(nameof(keyAgreement));
        _time                    = time                    ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Build and submit a trustee attestation for <paramref name="request"/>.
    /// Returns the coordinator outcome flattened for UI consumption.
    /// </summary>
    public async Task<AttestationSubmissionResult> SubmitAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trusteeNodeId = _nodeIdentity.Current.NodeId;
        var attestedAt    = _time.GetUtcNow();
        var requestHash   = TrusteeAttestation.HashOf(request);

        // 1) Fetch the owner-delivered envelope for this trustee. No
        //    envelope → owner never designated me, or the setup record
        //    was wiped. We CANNOT contribute to recovery without the
        //    seed; abort with Accepted=false.
        var envelope = await _recovery
            .GetTrusteeEncryptedSeedAsync(trusteeNodeId, cancellationToken)
            .ConfigureAwait(false);
        if (envelope is null)
        {
            return new AttestationSubmissionResult(
                Accepted: false, QuorumReached: false, GracePeriodStartedAt: null);
        }

        // 1a) Envelope wire-format validation. A malformed
        //    TrusteeEncryptedSeed (corrupted state-store, partial
        //    write) would otherwise escape OpenBox as ArgumentException
        //    and surface its raw "X25519 public key must be 32 bytes
        //    (was N)" to the user. Council BLOCKING-1: drop silently
        //    here, log nothing user-visible.
        if (envelope.OwnerEphX25519PublicKey is null
            || envelope.OwnerEphX25519PublicKey.Length != _keyAgreement.PublicKeyLength
            || envelope.Ciphertext is null
            || envelope.Ciphertext.Length != TrusteeAttestation.SeedEnvelopeCiphertextLength
            || envelope.Nonce is null
            || envelope.Nonce.Length != _keyAgreement.NonceLength)
        {
            return new AttestationSubmissionResult(
                Accepted: false, QuorumReached: false, GracePeriodStartedAt: null);
        }

        // 2) Derive the trustee's per-team X25519 keypair. The PUBLIC
        //    half MUST match what the owner recorded in
        //    TrusteeDesignation.DHPublicKey (MAJOR-2 binding, enforced
        //    coordinator-side via FixedTimeEquals). The PRIVATE half is
        //    what we use to OpenBox the owner-delivered envelope.
        var active = _activeTeam.Active
            ?? throw new InvalidOperationException(
                "No active team selected. Pick a team before approving recovery.");
        var rootSeed = await _rootSeedProvider.GetRootSeedAsync(cancellationToken).ConfigureAwait(false);
        var teamId   = active.TeamId.Value.ToString("D");
        // Council R-1 fix in HkdfX25519SubkeyDerivation.DeriveX25519PublicKey
        // now zeros its intermediate buffer, so the explicit inline-NSec
        // workaround is no longer required — use the simpler API.
        // R-3: derive both keys INSIDE the try-block so the finally's
        // ZeroMemory still runs if either derivation throws.
        byte[]? trusteeDhPriv = null;
        byte[]? trusteeDhPub  = null;
        byte[]? recoveredSeed = null;
        byte[]? reEncryptedCiphertext = null;
        byte[]? reEncryptedNonce = null;
        try
        {
            trusteeDhPriv = _x25519SubkeyDerivation.DeriveX25519PrivateKey(rootSeed, teamId);
            trusteeDhPub  = _x25519SubkeyDerivation.DeriveX25519PublicKey(rootSeed, teamId);
            // 3) OpenBox the owner-delivered envelope. Sender = owner
            //    ephemeral X25519 pub (recorded in the envelope).
            //    Recipient = this trustee's per-team X25519 private key.
            //    X25519KeyAgreement.OpenBox may throw CryptographicException
            //    when the peer key fails RFC 7748 contributory checks
            //    (e.g. all-zero attempt) — treat that the same as a
            //    silent auth-tag failure: abort.
            try
            {
                recoveredSeed = _keyAgreement.OpenBox(
                    ciphertext:           envelope.Ciphertext,
                    nonce:                envelope.Nonce,
                    senderPublicKey:      envelope.OwnerEphX25519PublicKey,
                    recipientPrivateKey:  trusteeDhPriv);
            }
            catch (CryptographicException)
            {
                return new AttestationSubmissionResult(
                    Accepted: false, QuorumReached: false, GracePeriodStartedAt: null);
            }
            catch (ArgumentException)
            {
                // Belt-and-braces against any wire-format mismatch
                // that slipped past the upfront envelope validation
                // (e.g., NSec internal length-check failure on
                // platform-specific implementations).
                return new AttestationSubmissionResult(
                    Accepted: false, QuorumReached: false, GracePeriodStartedAt: null);
            }
            if (recoveredSeed is null)
            {
                // Owner-delivered envelope failed to decrypt — likely
                // the trustee's DH key rotated between setup and now,
                // or the envelope was tampered. Abort.
                return new AttestationSubmissionResult(
                    Accepted: false, QuorumReached: false, GracePeriodStartedAt: null);
            }

            // 4) Re-Box the seed toward the recovering device's
            //    ephemeral X25519 public key. Sender = this trustee's
            //    per-team X25519 private key (so the recovering device's
            //    completion handler can OpenBox using
            //    senderPublicKey=TrusteeDHPublicKey + its ephemeral
            //    private key).
            (reEncryptedCiphertext, reEncryptedNonce) = _keyAgreement.Box(
                plaintext:            recoveredSeed,
                recipientPublicKey:   request.EphemeralDHPublicKey,
                senderPrivateKey:     trusteeDhPriv);

            // 5) Build canonical signing bytes + sign with the trustee's
            //    per-team Ed25519 signer.
            var canonical = TrusteeAttestation.CanonicalBytesForSigning(
                trusteeNodeId, requestHash, attestedAt,
                trusteeDhPub, reEncryptedCiphertext, reEncryptedNonce);
            var signer = await _signerAccessor.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            var signature = await signer.SignAsync(canonical, cancellationToken).ConfigureAwait(false);

            var attestation = new TrusteeAttestation(
                TrusteeNodeId:                    trusteeNodeId,
                TrusteePublicKey:                 signer.PublicKey.ToArray(),
                RecoveryRequestHash:              requestHash,
                AttestedAt:                       attestedAt,
                Signature:                        signature,
                TrusteeDHPublicKey:               trusteeDhPub,
                EncryptedSeedEnvelopeCiphertext:  reEncryptedCiphertext,
                EncryptedSeedEnvelopeNonce:       reEncryptedNonce);

            var outcome = await _recovery
                .SubmitAttestationAsync(attestation, cancellationToken)
                .ConfigureAwait(false);

            var quorumEvent = outcome.Events
                .FirstOrDefault(e => e.Type == RecoveryEventType.GracePeriodStarted);

            return new AttestationSubmissionResult(
                Accepted:             outcome.Accepted,
                QuorumReached:        quorumEvent is not null,
                GracePeriodStartedAt: quorumEvent?.OccurredAt);
        }
        finally
        {
            // Zero secret material — recovered seed and trustee X25519
            // private key. The re-encrypted ciphertext is non-secret
            // (encrypted to the recovering device); leave it.
            if (trusteeDhPriv is not null) CryptographicOperations.ZeroMemory(trusteeDhPriv);
            if (recoveredSeed is not null) CryptographicOperations.ZeroMemory(recoveredSeed);
        }
    }
}

/// <summary>UI-shaped outcome of <see cref="RecoveryAttestationSubmitter.SubmitAsync"/>.</summary>
/// <param name="Accepted">Mirrors <see cref="RecoveryAttestationOutcome.Accepted"/>.</param>
/// <param name="QuorumReached"><c>true</c> if a <c>GracePeriodStarted</c> event fired on this submission.</param>
/// <param name="GracePeriodStartedAt">When the grace window began, or <c>null</c> if quorum was not yet reached.</param>
public sealed record AttestationSubmissionResult(
    bool Accepted,
    bool QuorumReached,
    DateTimeOffset? GracePeriodStartedAt);

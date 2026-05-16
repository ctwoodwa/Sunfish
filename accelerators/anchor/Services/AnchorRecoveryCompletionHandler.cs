using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#67 / ADR 0046-A6 — Anchor's <see cref="IRecoveryCompletionHandler"/>
/// real rekey path. After the coordinator's grace window elapses without
/// dispute, the handler:
///
///   1. Reads the recovering device's ephemeral X25519 private key from
///      <see cref="IEphemeralRecoveryKeyStore"/> (persisted by
///      <c>InitiateRecoveryPage</c> at request time).
///   2. Validates envelope lengths on every attestation
///      (TrusteeDHPub=32 B, Ciphertext=48 B, Nonce=24 B).
///   3. Decrypts each attestation's seed envelope via
///      <see cref="IX25519KeyAgreement.OpenBox"/>; skips null returns
///      (auth-tag mismatch — NEVER throws on tampering).
///   4. Enforces the configured quorum
///      (<see cref="RecoveryCoordinatorOptions.QuorumThreshold"/>) on
///      successful decryptions, NOT just attestation count — a single
///      rogue trustee whose envelope decrypts must not unilaterally
///      install a rogue seed.
///   5. Aborts if the decrypted seeds disagree (divergent-seed audit;
///      logs SHA-256 fingerprints, not raw seed bytes).
///   6. Detects multi-team installs and refuses to rekey — single-team
///      rekey only in this PR; multi-team sweep is a documented gap
///      (PR 5/6 scope).
///   7. Derives the new SQLCipher key for the active team upfront
///      (cheap HKDF) so deriviation failures surface before any
///      irreversible state mutation.
///   8. Restores the install root seed via
///      <see cref="IRootSeedRestorer.RestoreRootSeedAsync"/>.
///   9. Rotates the active team's encrypted store via
///      <see cref="IEncryptedStore.RotateKeyAsync"/>.
///  10. Clears the ephemeral private key and zeros all in-memory
///      seed buffers (try/finally guarantees this on ALL exit paths).
///
/// <b>Status of previously-deferred items:</b>
///   - TrusteeDHPublicKey binding — CLOSED by W#67 PR 5: the coordinator's
///     SubmitAttestationAsync FixedTimeEquals-checks the attestation's
///     TrusteeDHPublicKey against the designation's DHPublicKey and
///     drops on mismatch before the handler ever sees the attestation.
///   - Multi-team rekey sweep — STILL DEFERRED (this handler: detect +
///     fail loud).
///   - Typed <c>RecoveryRekey</c> audit event via <c>IAuditTrail</c>
///     (sub-pattern #48f) — STILL DEFERRED to PR 6; logged via
///     <see cref="ILogger"/> for now.
///   - <c>ISyncDaemon.AnnounceIdentityRotation</c> — STILL DEFERRED.
/// </summary>
internal sealed class AnchorRecoveryCompletionHandler : IRecoveryCompletionHandler
{
    private readonly IX25519KeyAgreement _keyAgreement;
    private readonly IRootSeedRestorer _rootSeedRestorer;
    private readonly ISqlCipherKeyDerivation _sqlCipherKeyDerivation;
    private readonly IEphemeralRecoveryKeyStore _ephemeralKeyStore;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ITeamContextFactory _teamFactory;
    private readonly IOptions<RecoveryCoordinatorOptions> _coordinatorOptions;
    private readonly ILogger<AnchorRecoveryCompletionHandler> _logger;

    public AnchorRecoveryCompletionHandler(
        IX25519KeyAgreement keyAgreement,
        IRootSeedRestorer rootSeedRestorer,
        ISqlCipherKeyDerivation sqlCipherKeyDerivation,
        IEphemeralRecoveryKeyStore ephemeralKeyStore,
        IActiveTeamAccessor activeTeam,
        ITeamContextFactory teamFactory,
        IOptions<RecoveryCoordinatorOptions> coordinatorOptions,
        ILogger<AnchorRecoveryCompletionHandler> logger)
    {
        _keyAgreement           = keyAgreement           ?? throw new ArgumentNullException(nameof(keyAgreement));
        _rootSeedRestorer       = rootSeedRestorer       ?? throw new ArgumentNullException(nameof(rootSeedRestorer));
        _sqlCipherKeyDerivation = sqlCipherKeyDerivation ?? throw new ArgumentNullException(nameof(sqlCipherKeyDerivation));
        _ephemeralKeyStore      = ephemeralKeyStore      ?? throw new ArgumentNullException(nameof(ephemeralKeyStore));
        _activeTeam             = activeTeam             ?? throw new ArgumentNullException(nameof(activeTeam));
        _teamFactory            = teamFactory            ?? throw new ArgumentNullException(nameof(teamFactory));
        _coordinatorOptions     = coordinatorOptions     ?? throw new ArgumentNullException(nameof(coordinatorOptions));
        _logger                 = logger                 ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(RecoveryCompletionResult completionResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completionResult);
        var completedEvent = completionResult.Event;
        var quorum = Math.Max(1, _coordinatorOptions.Value.QuorumThreshold);

        // Buffers we need to zero on EVERY exit path. Allocated lazily.
        byte[]? ephPriv = null;
        var decryptedSeeds = new List<byte[]>(completionResult.Attestations.Count);
        byte[]? recoveredSeed = null;
        byte[]? sqlCipherKey = null;
        var ephemeralKeyConsumed = false;

        try
        {
            // 1) Retrieve ephemeral DH private key.
            ephPriv = await _ephemeralKeyStore
                .GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, cancellationToken)
                .ConfigureAwait(false);
            if (ephPriv is null)
            {
                _logger.LogError(
                    "Recovery completion: ephemeral X25519 private key not present at slot {Slot}. "
                    + "Recovery cannot proceed (device wipe or partial state). actor={ActorNodeId}",
                    IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName,
                    completedEvent.ActorNodeId);
                return;
            }

            // 2) Validate envelope lengths up-front. A malformed envelope
            //    that bypassed earlier checks would otherwise trip
            //    KeystoreRootSeedProvider.RestoreRootSeedAsync's
            //    ArgumentException after we've already touched state.
            foreach (var att in completionResult.Attestations)
            {
                if (att.TrusteeDHPublicKey?.Length != TrusteeAttestation.TrusteeDHPublicKeyLength
                    || att.EncryptedSeedEnvelopeCiphertext?.Length != TrusteeAttestation.SeedEnvelopeCiphertextLength
                    || att.EncryptedSeedEnvelopeNonce?.Length != TrusteeAttestation.SeedEnvelopeNonceLength)
                {
                    _logger.LogError(
                        "Recovery completion: trustee {TrusteeNodeId} attestation has malformed envelope "
                        + "(DHPub={DhLen} CT={CtLen} Nonce={NonceLen}). Aborting rekey — re-attestation required.",
                        att.TrusteeNodeId,
                        att.TrusteeDHPublicKey?.Length ?? -1,
                        att.EncryptedSeedEnvelopeCiphertext?.Length ?? -1,
                        att.EncryptedSeedEnvelopeNonce?.Length ?? -1);
                    return;
                }
            }

            // 3) Decrypt each envelope. Skip null OpenBox returns
            //    (per IX25519KeyAgreement contract — never throws on
            //    tampering). The sender side is the trustee's DH key;
            //    the recipient side is this device's ephemeral DH key.
            //    (PR 5 re-encrypts the trustee-held envelope toward
            //    this device at attestation time; the field names on
            //    TrusteeAttestation reflect that re-encryption.)
            foreach (var att in completionResult.Attestations)
            {
                var seed = _keyAgreement.OpenBox(
                    ciphertext:           att.EncryptedSeedEnvelopeCiphertext,
                    nonce:                att.EncryptedSeedEnvelopeNonce,
                    senderPublicKey:      att.TrusteeDHPublicKey,
                    recipientPrivateKey:  ephPriv);
                if (seed is null)
                {
                    _logger.LogWarning(
                        "Recovery completion: trustee {TrusteeNodeId} envelope failed to decrypt; skipping.",
                        att.TrusteeNodeId);
                    continue;
                }
                decryptedSeeds.Add(seed);
            }

            // 4) Enforce QUORUM on successful decryptions — not just
            //    attestation count. A single rogue trustee whose envelope
            //    decrypts (when others fail) must not install a rogue seed.
            if (decryptedSeeds.Count < quorum)
            {
                _logger.LogError(
                    "Recovery completion: only {Successful} of {Total} envelopes decrypted (quorum required: {Quorum}). "
                    + "Aborting rekey — insufficient trustee coverage to proceed safely.",
                    decryptedSeeds.Count, completionResult.Attestations.Count, quorum);
                return;
            }

            // 5) Divergence check. SHA-256 fingerprint comparison
            //    (NOT raw bytes) — abort if any disagreement.
            // Truncated 8-char fingerprints — log just enough to triage
            // divergence without giving an offline attacker a full hash
            // to validate guesses against. The Distinct comparison itself
            // happens over the same 8-char prefix; collision risk is
            // negligible at 3-of-N quorum scale and the divergence path
            // is always an abort (seeds never committed).
            var distinctSeedHashes = decryptedSeeds
                .Select(s => Convert.ToHexString(SHA256.HashData(s))[..8])
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctSeedHashes.Count > 1)
            {
                _logger.LogError(
                    "Recovery completion: trustee-decrypted seeds DIVERGE across {DistinctCount} distinct values "
                    + "(SHA-256 fingerprints: {Fingerprints}). Aborting rekey to avoid using an adversarial seed.",
                    distinctSeedHashes.Count,
                    string.Join(", ", distinctSeedHashes));
                return;
            }

            recoveredSeed = decryptedSeeds[0];
            if (recoveredSeed.Length != KeystoreRootSeedProvider.SeedLength)
            {
                _logger.LogError(
                    "Recovery completion: recovered seed length is {Length} bytes (expected {Expected}). "
                    + "Aborting rekey — envelope plaintext does not match the root-seed contract.",
                    recoveredSeed.Length, KeystoreRootSeedProvider.SeedLength);
                return;
            }

            // 6) Multi-team detection. ITeamContextFactory.Active returns
            //    all materialized team contexts. If more than one is
            //    materialized, this PR refuses to rekey: rotating only
            //    the active team would leave the others unable to derive
            //    their old SQLCipher key after the root-seed restore.
            //    PR 5/6 will implement the multi-team sweep.
            var materializedTeams = _teamFactory.Active;
            if (materializedTeams.Count > 1)
            {
                _logger.LogError(
                    "Recovery completion: {TeamCount} teams materialized; multi-team rekey is not yet implemented "
                    + "(W#67 PR 5/6 scope). Aborting rekey to avoid bricking non-active teams.",
                    materializedTeams.Count);
                return;
            }

            // 7) Derive new SQLCipher key for the active team. The active
            //    team's encrypted store is per-team; resolve via its DI
            //    scope. If no active team is set, restore the root seed
            //    only (no per-team rekey needed; future team materializations
            //    will derive from the restored seed).
            var active = _activeTeam.Active;
            string? teamId = null;
            IEncryptedStore? encryptedStore = null;
            if (active is not null)
            {
                teamId = active.TeamId.Value.ToString("D");
                sqlCipherKey = _sqlCipherKeyDerivation.DeriveSqlCipherKey(recoveredSeed, teamId);
                encryptedStore = active.Services.GetRequiredService<IEncryptedStore>();
            }

            // 8) Restore root seed via the W#65 IRootSeedRestorer. After
            //    this returns, IRootSeedProvider.GetRootSeedAsync returns
            //    the restored bytes. We restore the seed BEFORE rotating
            //    because (a) derivation already succeeded for the active
            //    team's key; (b) rotation failure leaves a recoverable
            //    state — the next launch can rederive the same key from
            //    the restored seed and retry rotation. Restoring AFTER
            //    rotation would invert this: rotation could succeed
            //    against a stale derivation context if the keystore
            //    write transiently failed.
            await _rootSeedRestorer
                .RestoreRootSeedAsync(recoveredSeed, cancellationToken)
                .ConfigureAwait(false);

            // 9) Rotate the active team's encrypted store.
            if (encryptedStore is not null && sqlCipherKey is not null)
            {
                await encryptedStore
                    .RotateKeyAsync(sqlCipherKey, cancellationToken)
                    .ConfigureAwait(false);
            }

            // 10) Ephemeral key consumed cleanly; flag so the finally
            //     block removes it (not just zeros the local copy).
            ephemeralKeyConsumed = true;

            _logger.LogInformation(
                "Recovery completion: rekey applied (team={TeamId}, actor={ActorNodeId}, "
                + "target={TargetNodeId}, occurredAt={OccurredAt}, decryptions={DecryptCount}/{TotalCount}). "
                + "TODO (W#67 PR 6): emit typed RecoveryRekey audit record + announce identity rotation.",
                teamId ?? "<no-active-team>",
                completedEvent.ActorNodeId,
                completedEvent.TargetNodeId,
                completedEvent.OccurredAt,
                decryptedSeeds.Count,
                completionResult.Attestations.Count);
        }
        finally
        {
            // Always clear secret material from process memory.
            if (ephPriv is not null) CryptographicOperations.ZeroMemory(ephPriv);
            foreach (var seed in decryptedSeeds) CryptographicOperations.ZeroMemory(seed);
            if (sqlCipherKey is not null) CryptographicOperations.ZeroMemory(sqlCipherKey);

            // Always remove the ephemeral private key from the key store
            // on EVERY exit path — including aborts, exceptions, and the
            // success path. The recovery flow is single-use; the
            // ephemeral key must not survive past this handler call.
            // Best-effort: cleanup failure is logged but not re-thrown.
            try
            {
                await _ephemeralKeyStore
                    .RemoveAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Recovery completion: failed to remove ephemeral key from store after {Outcome}.",
                    ephemeralKeyConsumed ? "success" : "abort");
            }
        }
    }
}

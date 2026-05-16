using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
///   2. Decrypts each trustee attestation's seed envelope via
///      <see cref="IX25519KeyAgreement.OpenBox"/> using that private key.
///   3. Aborts if fewer than one envelope decrypts (no recoverable seed).
///   4. Aborts if the decrypted seeds disagree (divergent-seed audit).
///   5. Restores the install root seed via
///      <see cref="IRootSeedRestorer.RestoreRootSeedAsync"/>.
///   6. Derives the new SQLCipher key via
///      <see cref="ISqlCipherKeyDerivation.DeriveSqlCipherKey"/> and
///      rotates the active team's encrypted store via
///      <see cref="IEncryptedStore.RotateKeyAsync"/>.
///   7. Removes the ephemeral private key from the key store.
///
/// <b>Deferred to W#67 PR 6 (audit + sync broadcast):</b>
///   - Emit a typed <c>RecoveryRekey</c> audit event via <c>IAuditTrail</c>
///     (sub-pattern #48f) — currently logged via <see cref="ILogger"/>.
///   - Announce the rotated identity to peers via
///     <c>ISyncDaemon.AnnounceIdentityRotation</c>.
/// </summary>
internal sealed class AnchorRecoveryCompletionHandler : IRecoveryCompletionHandler
{
    private readonly IX25519KeyAgreement _keyAgreement;
    private readonly IRootSeedRestorer _rootSeedRestorer;
    private readonly ISqlCipherKeyDerivation _sqlCipherKeyDerivation;
    private readonly IEphemeralRecoveryKeyStore _ephemeralKeyStore;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ILogger<AnchorRecoveryCompletionHandler> _logger;

    public AnchorRecoveryCompletionHandler(
        IX25519KeyAgreement keyAgreement,
        IRootSeedRestorer rootSeedRestorer,
        ISqlCipherKeyDerivation sqlCipherKeyDerivation,
        IEphemeralRecoveryKeyStore ephemeralKeyStore,
        IActiveTeamAccessor activeTeam,
        ILogger<AnchorRecoveryCompletionHandler> logger)
    {
        _keyAgreement           = keyAgreement           ?? throw new ArgumentNullException(nameof(keyAgreement));
        _rootSeedRestorer       = rootSeedRestorer       ?? throw new ArgumentNullException(nameof(rootSeedRestorer));
        _sqlCipherKeyDerivation = sqlCipherKeyDerivation ?? throw new ArgumentNullException(nameof(sqlCipherKeyDerivation));
        _ephemeralKeyStore      = ephemeralKeyStore      ?? throw new ArgumentNullException(nameof(ephemeralKeyStore));
        _activeTeam             = activeTeam             ?? throw new ArgumentNullException(nameof(activeTeam));
        _logger                 = logger                 ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(RecoveryCompletionResult completionResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completionResult);
        var completedEvent = completionResult.Event;

        // 1) Retrieve ephemeral DH private key from the key store. If
        //    absent, the device's secure storage was wiped between
        //    initiation and completion — recovery cannot proceed; log
        //    and return so the polling service doesn't re-attempt.
        var ephPriv = await _ephemeralKeyStore
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

        // 2) Decrypt each attestation envelope. Skip null OpenBox
        //    returns (per IX25519KeyAgreement contract OpenBox returns
        //    null on auth-tag mismatch — never throws on tampering).
        var decryptedSeeds = new List<byte[]>(completionResult.Attestations.Count);
        foreach (var att in completionResult.Attestations)
        {
            var seed = _keyAgreement.OpenBox(
                ciphertext:             att.EncryptedSeedEnvelopeCiphertext,
                nonce:                  att.EncryptedSeedEnvelopeNonce,
                senderPublicKey:        att.TrusteeDHPublicKey,
                recipientPrivateKey:    ephPriv);
            if (seed is null)
            {
                _logger.LogWarning(
                    "Recovery completion: trustee {TrusteeNodeId} envelope failed to decrypt; skipping.",
                    att.TrusteeNodeId);
                continue;
            }
            decryptedSeeds.Add(seed);
        }

        // 3) Require at least one successful decryption. With zero
        //    decryptions we cannot reconstruct the seed; abort + log.
        if (decryptedSeeds.Count == 0)
        {
            _logger.LogError(
                "Recovery completion: zero trustee envelopes decrypted successfully (of {Count} attestations). Aborting rekey.",
                completionResult.Attestations.Count);
            return;
        }

        // 4) Divergence check. If the decrypted seeds disagree, a
        //    trustee or attacker injected a malicious envelope. Log
        //    SHA-256 hashes of the distinct seeds — never the raw
        //    bytes — and abort.
        var distinctSeedHashes = decryptedSeeds
            .Select(s => Convert.ToHexString(SHA256.HashData(s)))
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

        var recoveredSeed = decryptedSeeds[0];

        // 5) Restore root seed via the W#65 IRootSeedRestorer. After
        //    this returns, subsequent IRootSeedProvider.GetRootSeedAsync
        //    calls return the restored bytes.
        await _rootSeedRestorer
            .RestoreRootSeedAsync(recoveredSeed, cancellationToken)
            .ConfigureAwait(false);

        // 6) Derive + rotate the SQLCipher key for the active team. The
        //    encrypted store is per-team; resolve it from the active
        //    team's service provider per the kernel-runtime
        //    DefaultTeamServiceRegistrar registration.
        var active = _activeTeam.Active;
        if (active is null)
        {
            _logger.LogError(
                "Recovery completion: no active team; cannot derive a SQLCipher key. Root seed restored but rekey deferred.");
            return;
        }

        var teamId = active.TeamId.Value.ToString("D");
        var sqlCipherKey = _sqlCipherKeyDerivation.DeriveSqlCipherKey(recoveredSeed, teamId);
        var encryptedStore = active.Services.GetRequiredService<IEncryptedStore>();
        await encryptedStore
            .RotateKeyAsync(sqlCipherKey, cancellationToken)
            .ConfigureAwait(false);

        // 7) Clear the ephemeral private key so it doesn't linger past
        //    the single-use lifetime of the recovery flow.
        await _ephemeralKeyStore
            .RemoveAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Recovery completion: SQLCipher rekey applied for team {TeamId} (actor={ActorNodeId}, "
            + "target={TargetNodeId}, occurredAt={OccurredAt}, attestations={Count}). "
            + "TODO (W#67 PR 6): emit typed RecoveryRekey audit record + announce identity rotation.",
            teamId,
            completedEvent.ActorNodeId,
            completedEvent.TargetNodeId,
            completedEvent.OccurredAt,
            completionResult.Attestations.Count);
    }
}

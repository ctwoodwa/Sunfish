namespace Sunfish.Kernel.Security.Session;

/// <summary>
/// Resolves an <see cref="IBoundEd25519Signer"/> pre-loaded with the active
/// team's device-identity private key. The key is derived via the ADR 0032
/// per-team-subkey pipeline: <c>IRootSeedProvider.GetRootSeedAsync</c> →
/// <c>ITeamSubkeyDerivation.DeriveTeamKeypair(rootSeed, activeTeamId)</c>.
///
/// W#65 — answer to cob-question 2026-05-16T04-42Z. Consumers (W#63
/// <c>ApproveRecoveryPage</c>; future trustee-attestation flows) get a
/// signer they can call without ever touching the raw private key bytes.
/// </summary>
public interface ISessionSignerAccessor
{
    /// <summary>
    /// Returns a signer bound to the active team's identity key.
    /// Throws when no team is active (e.g., onboarding not complete).
    /// </summary>
    /// <exception cref="InvalidOperationException">No team is currently active.</exception>
    ValueTask<IBoundEd25519Signer> GetCurrentAsync(CancellationToken cancellationToken = default);
}

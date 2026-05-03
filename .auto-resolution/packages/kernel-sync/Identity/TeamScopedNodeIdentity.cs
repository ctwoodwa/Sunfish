using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Sync.Identity;

/// <summary>
/// Derives a <em>team-scoped</em> <see cref="NodeIdentity"/> from a root
/// <see cref="NodeIdentity"/> and a team id, per ADR 0032 §Device identity.
/// </summary>
/// <remarks>
/// <para>
/// The root <see cref="NodeIdentity.NodeId"/> is preserved unchanged — node
/// identifiers are install-level, not team-level. The public + private keys in
/// the returned identity are the HKDF-derived Ed25519 subkey for this
/// (root, team) pair. Operators of different teams see different public keys
/// and cannot correlate cross-team membership.
/// </para>
/// <para>
/// This is a static helper (not a method on <see cref="NodeIdentity"/>) because
/// <see cref="NodeIdentity"/> lives in kernel-sync and must not take a
/// compile-time dependency on <see cref="ITeamSubkeyDerivation"/>. The helper
/// does the wiring on behalf of callers.
/// </para>
/// </remarks>
public static class TeamScopedNodeIdentity
{
    /// <summary>
    /// Derive a team-scoped <see cref="NodeIdentity"/>.
    /// </summary>
    /// <param name="rootIdentity">The install's root identity. Its private key is
    /// used as input key material to the HKDF derivation.</param>
    /// <param name="teamId">Team identifier (string form).</param>
    /// <param name="subkeyDerivation">The subkey derivation primitive.</param>
    /// <returns>A new <see cref="NodeIdentity"/> with the same <see cref="NodeIdentity.NodeId"/>
    /// but with team-specific <see cref="NodeIdentity.PublicKey"/> and
    /// <see cref="NodeIdentity.PrivateKey"/>.</returns>
    public static NodeIdentity Derive(
        NodeIdentity rootIdentity,
        string teamId,
        ITeamSubkeyDerivation subkeyDerivation)
    {
        ArgumentNullException.ThrowIfNull(rootIdentity);
        ArgumentException.ThrowIfNullOrEmpty(teamId);
        ArgumentNullException.ThrowIfNull(subkeyDerivation);

        var (teamPub, teamPriv) = subkeyDerivation.DeriveTeamKeypair(
            rootIdentity.PrivateKey, teamId);
        return new NodeIdentity(rootIdentity.NodeId, teamPub, teamPriv);
    }
}

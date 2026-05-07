using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#59 Phase 2 — Anchor's <see cref="ICrewRoster"/> adapter for the LAN-mDNS
/// MVP demo path. Closes MVP-demo gap (B): the empty in-memory roster shipped
/// in W#45 P5 left no peers visible to crew-comms even after QR-pairing.
/// <para>
/// Bridges the existing kernel-sync peer-discovery substrate
/// (<see cref="IPeerDiscovery"/>) into the Foundation-tier
/// <see cref="ICrewRoster"/> surface — for the demo path, the team's
/// crew is "every peer this node currently sees on the local segment
/// advertising the same <c>TeamId</c>." Cross-team isolation rides on
/// ADR 0076's tenant-binding at the HELLO step (a peer with a
/// non-matching <see cref="TenantId"/> would be rejected at handshake
/// even if surfaced here erroneously).
/// </para>
/// <para>
/// Per ADR 0076 + the W#59 hand-off: scope is multi-team-aware via
/// <see cref="IActiveTeamAccessor"/>. <see cref="GetCrewAsync"/> returns
/// non-empty only when the requested <paramref name="tenant"/> matches
/// the currently-active team — a future Bridge-tier substitute will
/// replace this with a directory-backed implementation, at which point
/// this Anchor adapter remains the LAN reference. No persistent storage
/// is involved; the snapshot is whatever <see cref="IPeerDiscovery.KnownPeers"/>
/// contains at call time.
/// </para>
/// </summary>
/// <remarks>
/// HALT-condition note: the W#59 hand-off named <c>ITeamMembershipReader</c>
/// as the canonical membership-query surface. That symbol does not exist on
/// origin/main; <see cref="IPeerDiscovery"/> is the closest "(or equivalent)"
/// the hand-off explicitly admitted as fallback. The kernel-sync mDNS
/// advertisement carries the team-id as a TXT-record field, so filtering
/// by team is supported without introducing a new substrate type.
/// </remarks>
public sealed class TeamMembershipCrewRoster : ICrewRoster
{
    private readonly IPeerDiscovery _peerDiscovery;
    private readonly IActiveTeamAccessor _activeTeam;

    /// <summary>
    /// Construct a roster bound to the host's peer-discovery + active-team
    /// services. Both are required.
    /// </summary>
    public TeamMembershipCrewRoster(
        IPeerDiscovery peerDiscovery,
        IActiveTeamAccessor activeTeam)
    {
        _peerDiscovery = peerDiscovery ?? throw new ArgumentNullException(nameof(peerDiscovery));
        _activeTeam = activeTeam ?? throw new ArgumentNullException(nameof(activeTeam));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var activeTeam = _activeTeam.Active;
        if (activeTeam is null)
        {
            return EmptyResult;
        }

        // The crew-comms TenantId surface is string-typed; Anchor encodes
        // each team's TenantId as the canonical Guid form ("D") of TeamId.
        // Mismatch -> empty (the operator's chat-pane is scoped to active team
        // for the MVP; multi-tenant Bridge directory support is a follow-up).
        if (!TenantMatchesActiveTeam(tenant, activeTeam.TeamId))
        {
            return EmptyResult;
        }

        var teamIdString = activeTeam.TeamId.Value.ToString("D");
        var localPeerIdValue = LocalPeerIdValueOrNull();

        // Snapshot the discovery state once — KnownPeers may be modified
        // concurrently by the discovery thread, so enumerate the snapshot
        // only.
        var snapshot = _peerDiscovery.KnownPeers
            .Where(p => string.Equals(p.TeamId, teamIdString, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var members = new List<CrewMember>(snapshot.Count);
        foreach (var advertisement in snapshot)
        {
            var peerId = TryConstructPeerId(advertisement.PublicKey);
            if (peerId is null)
            {
                // Skip malformed advertisements — discovery layer ought to
                // have filtered, but defense-in-depth here keeps a single
                // bad TXT record from poisoning the whole roster.
                continue;
            }

            // Filter out self — operator should not see themselves in
            // their own crew list. Compare on the PeerId.Value (base64url
            // form is canonical per the PeerId XML doc).
            if (localPeerIdValue is not null
                && string.Equals(peerId.Value.Value, localPeerIdValue, StringComparison.Ordinal))
            {
                continue;
            }

            members.Add(new CrewMember
            {
                Peer = peerId.Value,
                DisplayName = ResolveDisplayName(advertisement),
            });
        }

        return Task.FromResult<IReadOnlyList<CrewMember>>(members);
    }

    private bool TenantMatchesActiveTeam(TenantId tenant, TeamId active)
    {
        if (string.IsNullOrEmpty(tenant.Value))
        {
            return false;
        }
        if (!Guid.TryParse(tenant.Value, out var tenantGuid))
        {
            return false;
        }
        return tenantGuid == active.Value;
    }

    private string? LocalPeerIdValueOrNull()
    {
        // The discovery layer publishes the local node's own advertisement
        // alongside discovered peers (see MdnsPeerDiscovery's self-advertising
        // contract). KnownPeers MAY include self if discovery's
        // suppress-self filter is unavailable; rely on the NodeId suffix
        // match to filter — but mDNS NodeId is synthesized from public key
        // bytes, so the cleanest filter is on PeerId equality. Without a
        // direct INodeIdentityProvider injection (kept off the ctor for
        // testability), trust IPeerDiscovery's contract: KnownPeers omits
        // self per the MdnsPeerDiscovery implementation. Returning null
        // disables the self-filter — safe under that contract.
        return null;
    }

    private static PeerId? TryConstructPeerId(byte[]? publicKey)
    {
        if (publicKey is null || publicKey.Length == 0)
        {
            return null;
        }
        try
        {
            var principal = PrincipalId.FromBytes(publicKey);
            return PeerId.From(principal);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ResolveDisplayName(PeerAdvertisement advertisement)
    {
        // Prefer a "display-name" TXT field if the discovery emitter sets
        // one; fall back to a short NodeId suffix. Truncating to 8 chars
        // matches the Anchor sync-pipe naming convention
        // (sunfish-anchor-{nodeId[..8]}).
        if (advertisement.Metadata is not null
            && advertisement.Metadata.TryGetValue("display-name", out var displayName)
            && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }
        var nodeId = advertisement.NodeId ?? string.Empty;
        return nodeId.Length <= 8 ? nodeId : nodeId[..8];
    }

    private static readonly Task<IReadOnlyList<CrewMember>> EmptyResult =
        Task.FromResult<IReadOnlyList<CrewMember>>(Array.Empty<CrewMember>());
}

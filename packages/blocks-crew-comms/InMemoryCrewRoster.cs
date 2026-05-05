using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;

namespace Sunfish.Blocks.CrewComms;

/// <summary>
/// In-memory <see cref="ICrewRoster"/> seeded at construction time. Used by
/// Anchor's Phase-1 wiring (single-tenant, hard-coded crew) and by every
/// integration test in this package. Per ADR 0076 — production deployments
/// replace this with a persistent roster (tenant directory; magic-link
/// invitations).
/// </summary>
public sealed class InMemoryCrewRoster : ICrewRoster
{
    private readonly IReadOnlyList<CrewMember> _members;

    /// <summary>
    /// Creates a roster pre-populated with the supplied crew members.
    /// Throws on duplicate <c>PeerId</c> in the seed — silent acceptance
    /// would let a misconfigured tenant register two distinct display
    /// names against the same Ed25519 identity (council finding #13).
    /// </summary>
    public InMemoryCrewRoster(IEnumerable<CrewMember> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var list = new List<CrewMember>();
        var peers = new HashSet<Sunfish.Federation.Common.PeerId>();
        foreach (var member in seed)
        {
            if (!peers.Add(member.Peer))
                throw new ArgumentException(
                    $"Duplicate PeerId {member.Peer} in roster seed.", nameof(seed));
            list.Add(member);
        }
        _members = list;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
        => Task.FromResult(_members);
}

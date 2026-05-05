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

    /// <summary>Creates a roster pre-populated with the supplied crew members.</summary>
    public InMemoryCrewRoster(IEnumerable<CrewMember> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _members = new List<CrewMember>(seed);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
        => Task.FromResult(_members);
}

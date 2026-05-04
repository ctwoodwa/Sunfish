using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Crew roster — the authoritative list of peers a tenant is allowed to
/// invite to a channel. Implementations source from the tenant's directory
/// substrate (e.g., Anchor's local user store; Bridge's tenant DB). Per
/// ADR 0076.
/// </summary>
public interface ICrewRoster
{
    /// <summary>
    /// Resolve every crew member registered under <paramref name="tenant"/>.
    /// Order is implementation-defined but stable across calls in the same
    /// process; consumers MUST NOT rely on a specific ordering.
    /// </summary>
    /// <param name="tenant">Tenant scope to query.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct);
}

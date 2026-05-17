using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Cross-cluster read accessor for party identity. Minimal surface for
/// resolving display names when this cluster doesn't own the party
/// canonical store. If <c>blocks-people-foundation</c> exposes its
/// own <see cref="IPartyReadModel"/>, hosts wire it via
/// <c>AddSingleton</c> (overriding the default <c>TryAddSingleton</c>).
/// </summary>
public interface IPartyReadModel
{
    Task<string?> GetDisplayNameAsync(
        TenantId tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default);
}

/// <summary>Fallback stub — returns null for every lookup (orphan-tolerant per CRDT §12).</summary>
internal sealed class InMemoryPartyReadModel : IPartyReadModel
{
    public Task<string?> GetDisplayNameAsync(TenantId tenantId, Guid partyId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

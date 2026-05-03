using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Authoritative registry of tenants the host knows about. Callers enumerate
/// or resolve by identity; writes typically happen through a host-specific
/// provisioning path (Bridge admin, seed configuration, tests).
/// </summary>
public interface ITenantCatalog
{
    /// <summary>Returns every registered tenant, in registration order.</summary>
    ValueTask<IReadOnlyList<TenantMetadata>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Tries to resolve one tenant by id.</summary>
    ValueTask<TenantMetadata?> TryGetAsync(
        TenantId id,
        CancellationToken cancellationToken = default);
}

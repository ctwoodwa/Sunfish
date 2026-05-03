using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Resolves the edition key (<c>lite</c>, <c>standard</c>, <c>enterprise</c>, …)
/// associated with a tenant. Consumed by entitlement resolvers and by Bridge
/// admin surfaces.
/// </summary>
public interface IEditionResolver
{
    /// <summary>Returns the edition key for a tenant, or <c>null</c> when unknown.</summary>
    ValueTask<string?> ResolveEditionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

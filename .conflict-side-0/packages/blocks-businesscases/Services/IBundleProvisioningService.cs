using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.BusinessCases.Services;

/// <summary>
/// Write-side contract that activates or deactivates a bundle for a tenant.
/// Implementations validate against <c>IBundleCatalog</c>, check deployment mode,
/// record the activation, and trigger feature-default seeding.
/// </summary>
public interface IBundleProvisioningService
{
    /// <summary>
    /// Activates <paramref name="bundleKey"/> at <paramref name="edition"/> for
    /// <paramref name="tenantId"/>. Validates the bundle exists in the catalog,
    /// validates the edition appears in the bundle's <c>EditionMappings</c>,
    /// and records a <see cref="BundleActivationRecord"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the bundle is unknown, the edition is not declared by the bundle,
    /// or an active record already exists for the tenant/bundle pair.
    /// </exception>
    ValueTask<BundleActivationRecord> ProvisionAsync(
        TenantId tenantId,
        string bundleKey,
        string edition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates <paramref name="bundleKey"/> for <paramref name="tenantId"/>.
    /// No-op if no active record exists.
    /// </summary>
    ValueTask DeprovisionAsync(
        TenantId tenantId,
        string bundleKey,
        CancellationToken cancellationToken = default);
}

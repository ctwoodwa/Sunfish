using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.BusinessCases.Services;

/// <summary>
/// Read-only contract for querying the business-case state of a tenant:
/// which bundle is active, which editions a bundle exposes, and which modules
/// an edition activates. Writes go through <see cref="IBundleProvisioningService"/>.
/// </summary>
public interface IBusinessCaseService
{
    /// <summary>
    /// Returns a <see cref="TenantEntitlementSnapshot"/> describing the tenant's
    /// currently active bundle, edition, modules, and feature defaults. Returns
    /// a snapshot with <see cref="TenantEntitlementSnapshot.ActiveBundleKey"/>
    /// set to <see langword="null"/> when no bundle is active for the tenant.
    /// </summary>
    ValueTask<TenantEntitlementSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the edition keys declared by the bundle with <paramref name="bundleKey"/>'s
    /// <c>EditionMappings</c>, or an empty list if the bundle is unknown.
    /// </summary>
    ValueTask<IReadOnlyList<string>> ListAvailableEditionsAsync(
        string bundleKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the module keys activated for a given bundle/edition — i.e.
    /// the union of the bundle's <c>RequiredModules</c> and the edition-mapped modules.
    /// Returns an empty list when the bundle or edition is unknown.
    /// </summary>
    ValueTask<IReadOnlyList<string>> ResolveModulesForEditionAsync(
        string bundleKey,
        string edition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the tenant's current <see cref="BundleActivationRecord"/>, or null
    /// when no bundle is active. Used by <c>BundleEntitlementResolver</c>.
    /// </summary>
    ValueTask<BundleActivationRecord?> GetActiveRecordAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

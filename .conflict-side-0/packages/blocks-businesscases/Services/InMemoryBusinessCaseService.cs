using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Blocks.BusinessCases.Services;

/// <summary>
/// In-memory implementation of <see cref="IBusinessCaseService"/>. Reads current
/// activation state from <see cref="InMemoryBundleActivationStore"/> and bundle
/// metadata from <see cref="IBundleCatalog"/>. Not intended for production.
/// </summary>
public sealed class InMemoryBusinessCaseService : IBusinessCaseService
{
    private readonly IBundleCatalog _catalog;
    private readonly InMemoryBundleActivationStore _store;

    /// <summary>Creates a new service backed by the given catalog and activation store.</summary>
    public InMemoryBusinessCaseService(IBundleCatalog catalog, InMemoryBundleActivationStore store)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public ValueTask<TenantEntitlementSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var record = _store.GetFirstActive(tenantId);
        if (record is null || !_catalog.TryGet(record.BundleKey, out var manifest) || manifest is null)
        {
            return ValueTask.FromResult(new TenantEntitlementSnapshot(
                tenantId,
                ActiveBundleKey: null,
                ActiveEdition: null,
                ActiveModules: Array.Empty<string>(),
                ResolvedFeatureValues: new Dictionary<string, string>(),
                ResolvedAt: DateTimeOffset.UtcNow));
        }

        var modules = MergeModules(manifest, record.Edition);
        var features = new Dictionary<string, string>(manifest.FeatureDefaults, StringComparer.Ordinal);

        return ValueTask.FromResult(new TenantEntitlementSnapshot(
            tenantId,
            ActiveBundleKey: record.BundleKey,
            ActiveEdition: record.Edition,
            ActiveModules: modules,
            ResolvedFeatureValues: features,
            ResolvedAt: DateTimeOffset.UtcNow));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ListAvailableEditionsAsync(
        string bundleKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_catalog.TryGet(bundleKey, out var manifest) || manifest is null)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(manifest.EditionMappings.Keys.ToArray());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ResolveModulesForEditionAsync(
        string bundleKey,
        string edition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(edition);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_catalog.TryGet(bundleKey, out var manifest) || manifest is null)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(MergeModules(manifest, edition));
    }

    /// <inheritdoc />
    public ValueTask<BundleActivationRecord?> GetActiveRecordAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_store.GetFirstActive(tenantId));
    }

    private static IReadOnlyList<string> MergeModules(BusinessCaseBundleManifest manifest, string edition)
    {
        var set = new HashSet<string>(manifest.RequiredModules, StringComparer.Ordinal);
        if (manifest.EditionMappings.TryGetValue(edition, out var mapped))
        {
            foreach (var module in mapped)
            {
                set.Add(module);
            }
        }

        return set.ToArray();
    }
}

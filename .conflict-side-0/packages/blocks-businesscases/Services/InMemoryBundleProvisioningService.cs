using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Blocks.BusinessCases.Services;

/// <summary>
/// In-memory implementation of <see cref="IBundleProvisioningService"/>. Validates
/// bundle and edition against <see cref="IBundleCatalog"/> and writes through to
/// a shared <see cref="InMemoryBundleActivationStore"/>. Not intended for production.
/// </summary>
public sealed class InMemoryBundleProvisioningService : IBundleProvisioningService
{
    private readonly IBundleCatalog _catalog;
    private readonly InMemoryBundleActivationStore _store;

    /// <summary>Creates a new service backed by the given catalog and activation store.</summary>
    public InMemoryBundleProvisioningService(IBundleCatalog catalog, InMemoryBundleActivationStore store)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public ValueTask<BundleActivationRecord> ProvisionAsync(
        TenantId tenantId,
        string bundleKey,
        string edition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(edition);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_catalog.TryGet(bundleKey, out var manifest) || manifest is null)
        {
            throw new InvalidOperationException(
                $"Bundle '{bundleKey}' is not registered in the catalog.");
        }

        if (!manifest.EditionMappings.ContainsKey(edition))
        {
            throw new InvalidOperationException(
                $"Edition '{edition}' is not declared by bundle '{bundleKey}'.");
        }

        if (_store.TryGet(tenantId, bundleKey, out var existing)
            && existing is not null
            && existing.DeactivatedAt is null)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId.Value}' already has an active activation for bundle '{bundleKey}'.");
        }

        var record = new BundleActivationRecord
        {
            Id = BundleActivationRecordId.NewId(),
            TenantId = tenantId,
            BundleKey = bundleKey,
            Edition = edition,
            ActivatedAt = DateTimeOffset.UtcNow,
            DeactivatedAt = null
        };

        _store.Upsert(record);
        return ValueTask.FromResult(record);
    }

    /// <inheritdoc />
    public ValueTask DeprovisionAsync(
        TenantId tenantId,
        string bundleKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleKey);
        cancellationToken.ThrowIfCancellationRequested();

        _store.Remove(tenantId, bundleKey);
        return ValueTask.CompletedTask;
    }
}

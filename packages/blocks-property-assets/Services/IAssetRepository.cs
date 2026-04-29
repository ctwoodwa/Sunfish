using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyAssets.Services;

/// <summary>
/// Domain repository for <see cref="Asset"/>. First-slice surface: get,
/// list (by tenant / property / class), upsert, soft-delete. Tenant-scoping
/// is mandatory on every call — the repository never returns assets from
/// other tenants.
/// </summary>
public interface IAssetRepository
{
    /// <summary>Returns the asset with the given id, or <c>null</c> if not found in the tenant's scope.</summary>
    Task<Asset?> GetByIdAsync(TenantId tenant, AssetId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all assets attached to the property, scoped to the tenant. By
    /// default excludes soft-deleted records.
    /// </summary>
    Task<IReadOnlyList<Asset>> ListByPropertyAsync(TenantId tenant, PropertyId property, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Lists all assets owned by the tenant. Disposed records excluded by default.</summary>
    Task<IReadOnlyList<Asset>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Lists all assets of the given class, scoped to the tenant. Disposed excluded by default.</summary>
    Task<IReadOnlyList<Asset>> ListByClassAsync(TenantId tenant, AssetClass assetClass, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates the asset.</summary>
    Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the asset by stamping <see cref="Asset.DisposedAt"/> and
    /// <see cref="Asset.DisposalReason"/>. ALSO appends an
    /// <see cref="AssetLifecycleEvent"/> of type
    /// <see cref="AssetLifecycleEventType.Disposed"/> via the registered
    /// <see cref="IAssetLifecycleEventStore"/>. No-op if the asset is unknown
    /// to the tenant.
    /// </summary>
    Task SoftDeleteAsync(TenantId tenant, AssetId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default);
}

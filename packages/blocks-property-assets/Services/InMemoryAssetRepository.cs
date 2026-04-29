using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyAssets.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IAssetRepository"/> for tests, demos,
/// and kitchen-sink scenarios. Persistence-backed implementations live
/// behind the same interface in their respective accelerator hosts.
/// </summary>
public sealed class InMemoryAssetRepository : IAssetRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, AssetId Id), Asset> _store = new();
    private readonly IAssetLifecycleEventStore _events;

    /// <summary>Create a repository wired to the given event store for soft-delete emission.</summary>
    public InMemoryAssetRepository(IAssetLifecycleEventStore events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public Task<Asset?> GetByIdAsync(TenantId tenant, AssetId id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((tenant, id), out var asset);
        return Task.FromResult(asset);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListByPropertyAsync(TenantId tenant, PropertyId property, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant) && kvp.Value.Property.Equals(property))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Asset> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Asset> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListByClassAsync(TenantId tenant, AssetClass assetClass, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant) && kvp.Value.Class == assetClass)
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Asset> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        _store[(asset.TenantId, asset.Id)] = asset;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(TenantId tenant, AssetId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordedBy);

        if (_store.TryGetValue((tenant, id), out var existing))
        {
            var disposed = existing with { DisposedAt = disposedAt, DisposalReason = reason };
            _store[(tenant, id)] = disposed;

            await _events.AppendAsync(new AssetLifecycleEvent
            {
                EventId = Guid.NewGuid(),
                Asset = id,
                Property = existing.Property,
                TenantId = tenant,
                EventType = AssetLifecycleEventType.Disposed,
                OccurredAt = disposedAt,
                RecordedBy = recordedBy,
                Notes = reason,
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}

using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyAssets.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IAssetLifecycleEventStore"/> for tests,
/// demos, and kitchen-sink scenarios. Persistence-backed implementations live
/// behind the same interface in their respective accelerator hosts.
/// </summary>
public sealed class InMemoryAssetLifecycleEventStore : IAssetLifecycleEventStore
{
    private readonly ConcurrentDictionary<(TenantId Tenant, AssetId Asset), List<AssetLifecycleEvent>> _byAsset = new();
    private readonly ConcurrentDictionary<(TenantId Tenant, PropertyId Property), List<AssetLifecycleEvent>> _byProperty = new();
    private readonly object _appendLock = new();

    /// <inheritdoc />
    public Task AppendAsync(AssetLifecycleEvent ev, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ev);

        lock (_appendLock)
        {
            var assetList = _byAsset.GetOrAdd((ev.TenantId, ev.Asset), _ => new List<AssetLifecycleEvent>());
            assetList.Add(ev);

            var propertyList = _byProperty.GetOrAdd((ev.TenantId, ev.Property), _ => new List<AssetLifecycleEvent>());
            propertyList.Add(ev);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AssetLifecycleEvent>> GetForAssetAsync(TenantId tenant, AssetId asset, CancellationToken cancellationToken = default)
        => Task.FromResult(Snapshot(_byAsset, (tenant, asset)));

    /// <inheritdoc />
    public Task<IReadOnlyList<AssetLifecycleEvent>> GetForPropertyAsync(TenantId tenant, PropertyId property, CancellationToken cancellationToken = default)
        => Task.FromResult(Snapshot(_byProperty, (tenant, property)));

    private IReadOnlyList<AssetLifecycleEvent> Snapshot<TKey>(ConcurrentDictionary<TKey, List<AssetLifecycleEvent>> index, TKey key)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            return Array.Empty<AssetLifecycleEvent>();
        }
        lock (_appendLock)
        {
            return list.OrderBy(e => e.OccurredAt).ToList();
        }
    }
}

using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IEquipmentLifecycleEventStore"/> for tests,
/// demos, and kitchen-sink scenarios. Persistence-backed implementations live
/// behind the same interface in their respective accelerator hosts.
/// </summary>
public sealed class InMemoryEquipmentLifecycleEventStore : IEquipmentLifecycleEventStore
{
    private readonly ConcurrentDictionary<(TenantId Tenant, EquipmentId Equipment), List<EquipmentLifecycleEvent>> _byEquipment = new();
    private readonly ConcurrentDictionary<(TenantId Tenant, PropertyId Property), List<EquipmentLifecycleEvent>> _byProperty = new();
    private readonly object _appendLock = new();

    /// <inheritdoc />
    public Task AppendAsync(EquipmentLifecycleEvent ev, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ev);

        lock (_appendLock)
        {
            var equipmentList = _byEquipment.GetOrAdd((ev.TenantId, ev.Equipment), _ => new List<EquipmentLifecycleEvent>());
            equipmentList.Add(ev);

            var propertyList = _byProperty.GetOrAdd((ev.TenantId, ev.Property), _ => new List<EquipmentLifecycleEvent>());
            propertyList.Add(ev);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EquipmentLifecycleEvent>> GetForEquipmentAsync(TenantId tenant, EquipmentId equipment, CancellationToken cancellationToken = default)
        => Task.FromResult(Snapshot(_byEquipment, (tenant, equipment)));

    /// <inheritdoc />
    public Task<IReadOnlyList<EquipmentLifecycleEvent>> GetForPropertyAsync(TenantId tenant, PropertyId property, CancellationToken cancellationToken = default)
        => Task.FromResult(Snapshot(_byProperty, (tenant, property)));

    private IReadOnlyList<EquipmentLifecycleEvent> Snapshot<TKey>(ConcurrentDictionary<TKey, List<EquipmentLifecycleEvent>> index, TKey key)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            return Array.Empty<EquipmentLifecycleEvent>();
        }
        lock (_appendLock)
        {
            return list.OrderBy(e => e.OccurredAt).ToList();
        }
    }
}

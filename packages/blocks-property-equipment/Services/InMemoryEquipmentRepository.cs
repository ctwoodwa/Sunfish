using System.Collections.Concurrent;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IEquipmentRepository"/> for tests, demos,
/// and kitchen-sink scenarios. Persistence-backed implementations live
/// behind the same interface in their respective accelerator hosts.
/// </summary>
public sealed class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, EquipmentId Id), Equipment> _store = new();
    private readonly IEquipmentLifecycleEventStore _events;

    /// <summary>Create a repository wired to the given event store for soft-delete emission.</summary>
    public InMemoryEquipmentRepository(IEquipmentLifecycleEventStore events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public Task<Equipment?> GetByIdAsync(TenantId tenant, EquipmentId id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((tenant, id), out var equipment);
        return Task.FromResult(equipment);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Equipment>> ListByPropertyAsync(TenantId tenant, PropertyId property, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant) && kvp.Value.Property.Equals(property))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Equipment> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Equipment>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Equipment> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Equipment>> ListByClassAsync(TenantId tenant, EquipmentClass equipmentClass, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant) && kvp.Value.Class == equipmentClass)
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Equipment> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpsertAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _store[(equipment.TenantId, equipment.Id)] = equipment;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(TenantId tenant, EquipmentId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordedBy);

        if (_store.TryGetValue((tenant, id), out var existing))
        {
            var disposed = existing with { DisposedAt = disposedAt, DisposalReason = reason };
            _store[(tenant, id)] = disposed;

            await _events.AppendAsync(new EquipmentLifecycleEvent
            {
                EventId = Guid.NewGuid(),
                Equipment = id,
                Property = existing.Property,
                TenantId = tenant,
                EventType = EquipmentLifecycleEventType.Disposed,
                OccurredAt = disposedAt,
                RecordedBy = recordedBy,
                Notes = reason,
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}

using System.Collections.Concurrent;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// In-memory <see cref="ITripStore"/>. Appends update the parent
/// <see cref="VehicleMetadata.CurrentOdometer"/> via the supplied
/// <see cref="IEquipmentRepository"/> so consumers see the latest reading
/// on subsequent equipment reads.
/// </summary>
public sealed class InMemoryTripStore : ITripStore
{
    private readonly ConcurrentDictionary<TripRecordId, TripRecord> _trips = new();
    // Per-equipment append-order log so GetForEquipmentAsync can return in
    // insertion order without sorting on a non-deterministic timestamp.
    private readonly ConcurrentDictionary<EquipmentId, List<TripRecordId>> _byEquipment = new();
    private readonly IEquipmentRepository _equipment;

    public InMemoryTripStore(IEquipmentRepository equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        _equipment = equipment;
    }

    /// <inheritdoc />
    public Task<TripRecord?> GetAsync(TripRecordId id, CancellationToken cancellationToken = default)
    {
        _trips.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TripRecord>> GetForEquipmentAsync(EquipmentId equipmentId, CancellationToken cancellationToken = default)
    {
        if (!_byEquipment.TryGetValue(equipmentId, out var ids))
            return Task.FromResult<IReadOnlyList<TripRecord>>(Array.Empty<TripRecord>());

        // Copy under monitor to keep the snapshot stable across concurrent appends.
        TripRecordId[] snapshot;
        lock (ids)
            snapshot = ids.ToArray();

        var rows = new List<TripRecord>(snapshot.Length);
        foreach (var id in snapshot)
            if (_trips.TryGetValue(id, out var r))
                rows.Add(r);
        return Task.FromResult<IReadOnlyList<TripRecord>>(rows);
    }

    /// <inheritdoc />
    public async Task AppendAsync(TripRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.StartOdometer > record.EndOdometer)
            throw new ArgumentException(
                $"Trip {record.Id} has StartOdometer {record.StartOdometer} > EndOdometer {record.EndOdometer}; negative-miles guard.",
                nameof(record));

        _trips[record.Id] = record;
        var ids = _byEquipment.GetOrAdd(record.EquipmentId, _ => new List<TripRecordId>());
        lock (ids)
            ids.Add(record.Id);

        // Update parent equipment's CurrentOdometer to the latest EndOdometer.
        var equipment = await _equipment.GetByIdAsync(record.TenantId, record.EquipmentId, cancellationToken).ConfigureAwait(false);
        if (equipment is null) return;
        var vehicleData = equipment.VehicleData ?? new VehicleMetadata();
        if (vehicleData.CurrentOdometer < record.EndOdometer)
        {
            var updated = equipment with { VehicleData = vehicleData with { CurrentOdometer = record.EndOdometer } };
            await _equipment.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
        }
    }
}

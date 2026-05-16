using Sunfish.Blocks.PropertyEquipment.Models;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Append-only store for <see cref="TripRecord"/> entries. Implementations
/// MUST reject <see cref="TripRecord.StartOdometer"/> &gt;
/// <see cref="TripRecord.EndOdometer"/> (negative-miles guard) by throwing
/// <see cref="ArgumentException"/>.
///
/// Appending a trip is expected to also update the parent
/// <see cref="VehicleMetadata.CurrentOdometer"/> on the equipment record;
/// the in-memory implementation does this via the registered
/// <see cref="IEquipmentRepository"/>.
/// </summary>
public interface ITripStore
{
    /// <summary>Returns the trip with the given id, or <c>null</c> if not found.</summary>
    Task<TripRecord?> GetAsync(TripRecordId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every trip recorded for the equipment in append order
    /// (oldest first).
    /// </summary>
    Task<IReadOnlyList<TripRecord>> GetForEquipmentAsync(EquipmentId equipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a trip. Throws <see cref="ArgumentException"/> when
    /// <see cref="TripRecord.StartOdometer"/> &gt; <see cref="TripRecord.EndOdometer"/>.
    /// </summary>
    Task AppendAsync(TripRecord record, CancellationToken cancellationToken = default);
}

using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Domain repository for <see cref="Equipment"/>. First-slice surface: get,
/// list (by tenant / property / class), upsert, soft-delete. Tenant-scoping
/// is mandatory on every call — the repository never returns equipment from
/// other tenants.
/// </summary>
public interface IEquipmentRepository
{
    /// <summary>Returns the equipment with the given id, or <c>null</c> if not found in the tenant's scope.</summary>
    Task<Equipment?> GetByIdAsync(TenantId tenant, EquipmentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all equipment items attached to the property, scoped to the tenant. By
    /// default excludes soft-deleted records.
    /// </summary>
    Task<IReadOnlyList<Equipment>> ListByPropertyAsync(TenantId tenant, PropertyId property, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Lists all equipment items owned by the tenant. Disposed records excluded by default.</summary>
    Task<IReadOnlyList<Equipment>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Lists all equipment items of the given class, scoped to the tenant. Disposed excluded by default.</summary>
    Task<IReadOnlyList<Equipment>> ListByClassAsync(TenantId tenant, EquipmentClass equipmentClass, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates the equipment.</summary>
    Task UpsertAsync(Equipment equipment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the equipment by stamping <see cref="Equipment.DisposedAt"/> and
    /// <see cref="Equipment.DisposalReason"/>. ALSO appends an
    /// <see cref="EquipmentLifecycleEvent"/> of type
    /// <see cref="EquipmentLifecycleEventType.Disposed"/> via the registered
    /// <see cref="IEquipmentLifecycleEventStore"/>. No-op if the equipment is unknown
    /// to the tenant.
    /// </summary>
    Task SoftDeleteAsync(TenantId tenant, EquipmentId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default);
}

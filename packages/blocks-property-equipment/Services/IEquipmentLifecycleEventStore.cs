using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Services;

/// <summary>
/// Append-only event store for <see cref="EquipmentLifecycleEvent"/>. Reads are
/// always tenant-scoped; the store never returns events from other tenants.
/// </summary>
/// <remarks>
/// First-slice ships an in-memory implementation only. Full
/// <c>IAuditTrail</c> emission is deferred — see <see cref="EquipmentLifecycleEvent"/>
/// remarks.
/// </remarks>
public interface IEquipmentLifecycleEventStore
{
    /// <summary>Append a new event. Events are immutable after append.</summary>
    Task AppendAsync(EquipmentLifecycleEvent ev, CancellationToken cancellationToken = default);

    /// <summary>Returns events for the equipment, oldest first.</summary>
    Task<IReadOnlyList<EquipmentLifecycleEvent>> GetForEquipmentAsync(TenantId tenant, EquipmentId equipment, CancellationToken cancellationToken = default);

    /// <summary>Returns events for any equipment attached to the property, oldest first.</summary>
    Task<IReadOnlyList<EquipmentLifecycleEvent>> GetForPropertyAsync(TenantId tenant, PropertyId property, CancellationToken cancellationToken = default);
}

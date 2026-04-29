using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyAssets.Services;

/// <summary>
/// Append-only event store for <see cref="AssetLifecycleEvent"/>. Reads are
/// always tenant-scoped; the store never returns events from other tenants.
/// </summary>
/// <remarks>
/// First-slice ships an in-memory implementation only. Full
/// <c>IAuditTrail</c> emission is deferred — see <see cref="AssetLifecycleEvent"/>
/// remarks.
/// </remarks>
public interface IAssetLifecycleEventStore
{
    /// <summary>Append a new event. Events are immutable after append.</summary>
    Task AppendAsync(AssetLifecycleEvent ev, CancellationToken cancellationToken = default);

    /// <summary>Returns events for the asset, oldest first.</summary>
    Task<IReadOnlyList<AssetLifecycleEvent>> GetForAssetAsync(TenantId tenant, AssetId asset, CancellationToken cancellationToken = default);

    /// <summary>Returns events for any asset attached to the property, oldest first.</summary>
    Task<IReadOnlyList<AssetLifecycleEvent>> GetForPropertyAsync(TenantId tenant, PropertyId property, CancellationToken cancellationToken = default);
}

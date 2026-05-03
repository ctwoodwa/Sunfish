using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Persistence for <see cref="SyncCursor"/> records. Pull-based integration
/// adapters read and update cursors to resume cleanly after restarts.
/// </summary>
public interface ISyncCursorStore
{
    /// <summary>Returns the current cursor, or null when never stored.</summary>
    ValueTask<SyncCursor?> GetAsync(
        string providerKey,
        TenantId? tenantId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>Stores or replaces the cursor.</summary>
    ValueTask PutAsync(SyncCursor cursor, CancellationToken cancellationToken = default);
}

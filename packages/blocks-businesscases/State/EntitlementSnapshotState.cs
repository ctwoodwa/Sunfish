using Sunfish.Blocks.BusinessCases.Models;

namespace Sunfish.Blocks.BusinessCases.State;

/// <summary>
/// UI-side holder for a fetched <see cref="TenantEntitlementSnapshot"/>. Used by
/// the diagnostic block to track load state between render passes.
/// </summary>
public sealed record EntitlementSnapshotState(TenantEntitlementSnapshot? Snapshot, bool IsLoading)
{
    /// <summary>Initial state: not loaded.</summary>
    public static EntitlementSnapshotState Loading() => new(Snapshot: null, IsLoading: true);

    /// <summary>Snapshot loaded (may still contain null ActiveBundleKey if no bundle is active).</summary>
    public static EntitlementSnapshotState Loaded(TenantEntitlementSnapshot snapshot) =>
        new(Snapshot: snapshot, IsLoading: false);
}

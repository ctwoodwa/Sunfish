namespace Sunfish.UIAdapters.Blazor.Components.LocalFirst;

/// <summary>
/// Visual-vocabulary states from the Local-Node Architecture paper (§5.2, §13.2).
/// Bound 1:1 to the <c>sf-sync--*</c> CSS utilities emitted by every provider.
/// </summary>
/// <remarks>
/// <para>Tokens live in each provider's <c>foundation/_sync-state.scss</c>.</para>
/// <list type="bullet">
///   <item><description><see cref="Healthy"/> — sync is within the configured freshness threshold.</description></item>
///   <item><description><see cref="Stale"/> — beyond the threshold but still usable (paper §13.2).</description></item>
///   <item><description><see cref="Offline"/> — no transport or no signal.</description></item>
///   <item><description><see cref="ConflictPending"/> — user-actionable merge / lease / schema-epoch conflict.</description></item>
///   <item><description><see cref="Quarantine"/> — data flagged by the replication layer; non-actionable until operator review.</description></item>
/// </list>
/// </remarks>
public enum SyncState
{
    /// <summary>Sync is within the freshness threshold.</summary>
    Healthy,

    /// <summary>Beyond freshness threshold but still trusted.</summary>
    Stale,

    /// <summary>No transport or no signal.</summary>
    Offline,

    /// <summary>Merge conflict, lease contention, or schema-epoch mismatch awaiting resolution.</summary>
    ConflictPending,

    /// <summary>Data flagged by the replication layer; non-actionable until operator review.</summary>
    Quarantine,
}

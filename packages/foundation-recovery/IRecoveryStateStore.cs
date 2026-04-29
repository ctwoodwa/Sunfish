namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Persistence abstraction for <see cref="RecoveryCoordinatorState"/>.
/// </summary>
/// <remarks>
/// <para>
/// The Phase 1 implementation is <see cref="InMemoryRecoveryStateStore"/>;
/// production hosts wire a SQLCipher-backed implementation that survives
/// process restart (per the Phase 1 plan's "7-day grace must survive
/// device restart" requirement).
/// </para>
/// <para>
/// The store is the coordinator's only side-effect surface, so all I/O
/// failures bubble out of the async methods rather than being swallowed
/// by the coordinator. A failed save means the state mutation is rolled
/// back to the previously-loaded snapshot — the coordinator does not
/// proceed on partial persistence.
/// </para>
/// </remarks>
public interface IRecoveryStateStore
{
    /// <summary>
    /// Load the most-recently-persisted state, or
    /// <see cref="RecoveryCoordinatorState.Empty"/> if no state has yet
    /// been saved.
    /// </summary>
    Task<RecoveryCoordinatorState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replace the persisted state with
    /// <paramref name="state"/>.
    /// </summary>
    Task SaveAsync(RecoveryCoordinatorState state, CancellationToken cancellationToken = default);
}

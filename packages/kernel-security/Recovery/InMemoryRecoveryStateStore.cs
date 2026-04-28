namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// In-memory <see cref="IRecoveryStateStore"/> for tests and Phase 1
/// host wiring before a SQLCipher-backed implementation lands.
/// </summary>
/// <remarks>
/// State is held in a single field protected by a lock; <see cref="LoadAsync"/>
/// returns the current snapshot and <see cref="SaveAsync"/> overwrites it.
/// Not durable across process restart — production hosts must replace
/// this with a persistent implementation.
/// </remarks>
public sealed class InMemoryRecoveryStateStore : IRecoveryStateStore
{
    private readonly Lock _gate = new();
    private RecoveryCoordinatorState _state = RecoveryCoordinatorState.Empty;

    /// <inheritdoc />
    public Task<RecoveryCoordinatorState> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_state);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(RecoveryCoordinatorState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _state = state;
        }
        return Task.CompletedTask;
    }
}

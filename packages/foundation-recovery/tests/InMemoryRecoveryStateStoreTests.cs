using Sunfish.Foundation.Recovery;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// Direct coverage for <see cref="InMemoryRecoveryStateStore"/>. The store is
/// exercised indirectly by <see cref="RecoveryCoordinatorTests"/> via the
/// coordinator's mutate-and-persist loop, but the store contract itself
/// (initial state, round-trip, null-state rejection, cancellation
/// semantics) is worth pinning explicitly so a contributor swapping the
/// in-memory default for a SQLCipher-backed store has a regression
/// reference for the public surface of the abstraction.
/// </summary>
public sealed class InMemoryRecoveryStateStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_Empty_initially()
    {
        var store = new InMemoryRecoveryStateStore();
        var state = await store.LoadAsync();
        Assert.Same(RecoveryCoordinatorState.Empty, state);
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_returns_saved_instance()
    {
        var store = new InMemoryRecoveryStateStore();
        var snapshot = new RecoveryCoordinatorState { Disputed = true };

        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync();

        Assert.Same(snapshot, loaded);
    }

    [Fact]
    public async Task SaveAsync_overwrites_previous_state()
    {
        var store = new InMemoryRecoveryStateStore();
        var first = new RecoveryCoordinatorState { Disputed = true };
        var second = new RecoveryCoordinatorState { Completed = true };

        await store.SaveAsync(first);
        await store.SaveAsync(second);
        var loaded = await store.LoadAsync();

        Assert.Same(second, loaded);
    }

    [Fact]
    public async Task SaveAsync_throws_on_null_state()
    {
        var store = new InMemoryRecoveryStateStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_throws_on_cancelled_token()
    {
        var store = new InMemoryRecoveryStateStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.LoadAsync(cts.Token));
    }

    [Fact]
    public async Task SaveAsync_throws_on_cancelled_token()
    {
        var store = new InMemoryRecoveryStateStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.SaveAsync(RecoveryCoordinatorState.Empty, cts.Token));
    }
}

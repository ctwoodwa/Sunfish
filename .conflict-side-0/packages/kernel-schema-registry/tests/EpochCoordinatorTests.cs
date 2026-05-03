using Sunfish.Kernel.SchemaRegistry.Epochs;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Contract coverage for <see cref="EpochCoordinator"/> — initial state, announce,
/// cutover tracking, freeze, event emission, and current-epoch selection.
/// </summary>
public class EpochCoordinatorTests
{
    [Fact]
    public void InitialState_HasGenesisActiveEpoch()
    {
        var coord = new EpochCoordinator();

        Assert.Single(coord.Epochs);
        var only = coord.Epochs[0];
        Assert.Equal("epoch-1", only.Id);
        Assert.Equal(EpochStatus.Active, only.Status);
        Assert.Equal(string.Empty, only.PreviousId);
        Assert.Equal("epoch-1", coord.CurrentEpochId);
    }

    [Fact]
    public async Task AnnounceEpochAsync_AppendsAnnouncedRecord()
    {
        var coord = new EpochCoordinator();

        var id = await coord.AnnounceEpochAsync("field rename", CancellationToken.None);

        Assert.Equal("epoch-2", id);
        Assert.Equal(2, coord.Epochs.Count);
        Assert.Equal(EpochStatus.Announced, coord.Epochs[1].Status);
        Assert.Equal("epoch-1", coord.Epochs[1].PreviousId);
        Assert.Equal("field rename", coord.Epochs[1].ReasonSummary);
    }

    [Fact]
    public async Task AnnounceEpochAsync_RaisesEpochAnnouncedEvent()
    {
        var coord = new EpochCoordinator();
        EpochRecord? captured = null;
        coord.EpochAnnounced += (_, args) => captured = args.Epoch;

        await coord.AnnounceEpochAsync("reason", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("epoch-2", captured!.Id);
        Assert.Equal(EpochStatus.Announced, captured.Status);
    }

    [Fact]
    public async Task CurrentEpochId_IsLatestNonFrozen()
    {
        var coord = new EpochCoordinator();
        await coord.AnnounceEpochAsync("announced", CancellationToken.None);

        // Announced takes precedence over older Active as "current" — the latest
        // non-frozen epoch is the one new writes should be scoped against.
        Assert.Equal("epoch-2", coord.CurrentEpochId);
    }

    [Fact]
    public async Task RecordNodeCutoverAsync_AppendsNodeToEpochCutoverList()
    {
        var coord = new EpochCoordinator();
        await coord.AnnounceEpochAsync("reason", CancellationToken.None);

        await coord.RecordNodeCutoverAsync("node-a", "epoch-2", CancellationToken.None);
        await coord.RecordNodeCutoverAsync("node-b", "epoch-2", CancellationToken.None);
        // Duplicate call is idempotent.
        await coord.RecordNodeCutoverAsync("node-a", "epoch-2", CancellationToken.None);

        var epoch2 = coord.Epochs.Single(e => e.Id == "epoch-2");
        Assert.Equal(new[] { "node-a", "node-b" }, epoch2.CutoverNodes);
    }

    [Fact]
    public async Task RecordNodeCutoverAsync_UnknownEpoch_Throws()
    {
        var coord = new EpochCoordinator();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await coord.RecordNodeCutoverAsync("node-a", "epoch-999", CancellationToken.None));
    }

    [Fact]
    public async Task FreezeEpochAsync_TransitionsToFrozenAndRaisesEvent()
    {
        var coord = new EpochCoordinator();
        await coord.AnnounceEpochAsync("reason", CancellationToken.None);
        EpochRecord? captured = null;
        coord.EpochFrozen += (_, args) => captured = args.Epoch;

        await coord.FreezeEpochAsync("epoch-1", CancellationToken.None);

        var epoch1 = coord.Epochs.Single(e => e.Id == "epoch-1");
        Assert.Equal(EpochStatus.Frozen, epoch1.Status);
        Assert.NotNull(captured);
        Assert.Equal("epoch-1", captured!.Id);
        Assert.Equal(EpochStatus.Frozen, captured.Status);
    }

    [Fact]
    public async Task FreezeEpochAsync_PromotesAnnouncedToActiveWhenNoActiveLeft()
    {
        var coord = new EpochCoordinator();
        await coord.AnnounceEpochAsync("reason", CancellationToken.None);
        // Freeze the original Active epoch. The announced epoch-2 should become Active.
        await coord.FreezeEpochAsync("epoch-1", CancellationToken.None);

        var epoch2 = coord.Epochs.Single(e => e.Id == "epoch-2");
        Assert.Equal(EpochStatus.Active, epoch2.Status);
        Assert.Equal("epoch-2", coord.CurrentEpochId);
    }

    [Fact]
    public async Task FreezeEpochAsync_AlreadyFrozen_Throws()
    {
        var coord = new EpochCoordinator();
        await coord.AnnounceEpochAsync("reason", CancellationToken.None);
        await coord.FreezeEpochAsync("epoch-1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await coord.FreezeEpochAsync("epoch-1", CancellationToken.None));
    }
}

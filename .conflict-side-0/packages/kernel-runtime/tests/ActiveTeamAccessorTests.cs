using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class ActiveTeamAccessorTests
{
    [Fact]
    public void Initial_Active_is_null()
    {
        using var fixture = new Fixture();
        Assert.Null(fixture.Accessor.Active);
    }

    [Fact]
    public async Task SetActive_changes_Active_and_fires_event()
    {
        using var fixture = new Fixture();
        var id = TeamId.New();
        var ctx = await fixture.Factory.GetOrCreateAsync(id, "Acme", CancellationToken.None);

        ActiveTeamChangedEventArgs? captured = null;
        fixture.Accessor.ActiveChanged += (_, args) => captured = args;

        await fixture.Accessor.SetActiveAsync(id, CancellationToken.None);

        Assert.Same(ctx, fixture.Accessor.Active);
        Assert.NotNull(captured);
        Assert.Null(captured!.Previous);
        Assert.Same(ctx, captured.Current);
    }

    [Fact]
    public async Task SetActive_with_unknown_TeamId_throws()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Accessor.SetActiveAsync(TeamId.New(), CancellationToken.None));

        Assert.Null(fixture.Accessor.Active);
    }

    [Fact]
    public async Task Switching_between_teams_fires_event_with_previous_and_current()
    {
        using var fixture = new Fixture();
        var a = TeamId.New();
        var b = TeamId.New();

        var ctxA = await fixture.Factory.GetOrCreateAsync(a, "A", CancellationToken.None);
        var ctxB = await fixture.Factory.GetOrCreateAsync(b, "B", CancellationToken.None);

        await fixture.Accessor.SetActiveAsync(a, CancellationToken.None);

        ActiveTeamChangedEventArgs? captured = null;
        fixture.Accessor.ActiveChanged += (_, args) => captured = args;

        await fixture.Accessor.SetActiveAsync(b, CancellationToken.None);

        Assert.Same(ctxB, fixture.Accessor.Active);
        Assert.NotNull(captured);
        Assert.Same(ctxA, captured!.Previous);
        Assert.Same(ctxB, captured.Current);
    }

    [Fact]
    public async Task SetActive_to_same_team_does_not_fire_event()
    {
        using var fixture = new Fixture();
        var id = TeamId.New();
        await fixture.Factory.GetOrCreateAsync(id, "A", CancellationToken.None);
        await fixture.Accessor.SetActiveAsync(id, CancellationToken.None);

        var fireCount = 0;
        fixture.Accessor.ActiveChanged += (_, _) => Interlocked.Increment(ref fireCount);

        await fixture.Accessor.SetActiveAsync(id, CancellationToken.None);

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void Accessor_null_factory_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ActiveTeamAccessor(null!));
    }

    private sealed class Fixture : IDisposable
    {
        public TeamContextFactory Factory { get; }
        public ActiveTeamAccessor Accessor { get; }

        public Fixture()
        {
            Factory = new TeamContextFactory();
            Accessor = new ActiveTeamAccessor(Factory);
        }

        public void Dispose()
        {
            Factory.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

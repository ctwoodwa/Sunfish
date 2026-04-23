using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class TeamContextFactoryTests
{
    [Fact]
    public async Task GetOrCreate_creates_a_new_context_on_first_call()
    {
        await using var factory = new TeamContextFactory();
        var id = TeamId.New();

        var ctx = await factory.GetOrCreateAsync(id, "Acme", CancellationToken.None);

        Assert.NotNull(ctx);
        Assert.Equal(id, ctx.TeamId);
        Assert.Equal("Acme", ctx.DisplayName);
        Assert.Contains(ctx, factory.Active);
    }

    [Fact]
    public async Task Second_call_with_same_TeamId_returns_same_instance()
    {
        await using var factory = new TeamContextFactory();
        var id = TeamId.New();

        var first = await factory.GetOrCreateAsync(id, "Acme", CancellationToken.None);
        var second = await factory.GetOrCreateAsync(id, "Acme", CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(factory.Active);
    }

    [Fact]
    public async Task Concurrent_GetOrCreate_for_same_TeamId_returns_single_instance()
    {
        await using var factory = new TeamContextFactory();
        var id = TeamId.New();

        // Fan out 32 concurrent calls for the same team.
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => factory.GetOrCreateAsync(id, "Acme", CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Every returned context must be reference-identical.
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
        Assert.Single(factory.Active);
    }

    [Fact]
    public async Task Remove_disposes_the_context_and_removes_from_Active()
    {
        TeamServiceRegistrar registrar = (services, _) =>
        {
            // Register by type so the container owns + disposes the instance.
            services.AddSingleton<DisposeFlag>();
        };

        await using var factory = new TeamContextFactory(registrar);
        var id = TeamId.New();
        var ctx = await factory.GetOrCreateAsync(id, "Acme", CancellationToken.None);

        // Force resolution so the service graph is realized inside the provider.
        var flag = ctx.Services.GetRequiredService<DisposeFlag>();

        await factory.RemoveAsync(id, CancellationToken.None);

        Assert.Empty(factory.Active);
        Assert.True(flag.Disposed, "TeamContext disposal must cascade into the service provider.");
    }

    [Fact]
    public async Task Remove_unknown_team_is_no_op()
    {
        await using var factory = new TeamContextFactory();
        var id = TeamId.New();

        // Must not throw.
        await factory.RemoveAsync(id, CancellationToken.None);

        Assert.Empty(factory.Active);
    }

    [Fact]
    public async Task Active_reflects_current_set()
    {
        await using var factory = new TeamContextFactory();
        var a = TeamId.New();
        var b = TeamId.New();
        var c = TeamId.New();

        await factory.GetOrCreateAsync(a, "A", CancellationToken.None);
        await factory.GetOrCreateAsync(b, "B", CancellationToken.None);
        await factory.GetOrCreateAsync(c, "C", CancellationToken.None);

        Assert.Equal(3, factory.Active.Count);

        await factory.RemoveAsync(b, CancellationToken.None);

        Assert.Equal(2, factory.Active.Count);
        Assert.DoesNotContain(factory.Active, ctx => ctx.TeamId.Equals(b));
    }

    [Fact]
    public async Task TeamServiceRegistrar_is_invoked_with_expected_TeamId()
    {
        var seen = new List<TeamId>();
        TeamServiceRegistrar registrar = (_, teamId) => seen.Add(teamId);

        await using var factory = new TeamContextFactory(registrar);

        var a = TeamId.New();
        var b = TeamId.New();

        await factory.GetOrCreateAsync(a, "A", CancellationToken.None);
        await factory.GetOrCreateAsync(b, "B", CancellationToken.None);
        // Second get for same team must NOT re-invoke the registrar.
        await factory.GetOrCreateAsync(a, "A", CancellationToken.None);

        Assert.Equal(new[] { a, b }, seen);
    }

    [Fact]
    public async Task GetOrCreate_with_empty_displayName_throws()
    {
        await using var factory = new TeamContextFactory();
        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.GetOrCreateAsync(TeamId.New(), "", CancellationToken.None));
    }

    [Fact]
    public async Task GetOrCreate_with_cancelled_token_throws()
    {
        await using var factory = new TeamContextFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => factory.GetOrCreateAsync(TeamId.New(), "A", cts.Token));
    }

    [Fact]
    public async Task Dispose_cascades_to_all_live_contexts()
    {
        TeamServiceRegistrar registrar = (services, _) =>
        {
            // Container-owned singleton so the DI provider disposes it.
            services.AddSingleton<DisposeFlag>();
        };

        var factory = new TeamContextFactory(registrar);
        var a = TeamId.New();
        var b = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(a, "A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(b, "B", CancellationToken.None);
        var flagA = ctxA.Services.GetRequiredService<DisposeFlag>();
        var flagB = ctxB.Services.GetRequiredService<DisposeFlag>();

        Assert.NotSame(flagA, flagB);

        await factory.DisposeAsync();

        Assert.True(flagA.Disposed);
        Assert.True(flagB.Disposed);
    }

    private sealed class DisposeFlag : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}

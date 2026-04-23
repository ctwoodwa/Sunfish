using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.LocalNodeHost.Health;

namespace Sunfish.LocalNodeHost.Tests.Health;

/// <summary>
/// Unit tests for the Wave 5.2.D <see cref="LocalNodeHealthCheck"/>. Pins
/// the Healthy / Degraded / Unhealthy branches against the decomposition
/// plan §6 matrix.
/// </summary>
public class LocalNodeHealthCheckTests
{
    private static readonly TeamId TestTeamId = new(new Guid("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public async Task Returns_Unhealthy_when_no_active_team()
    {
        var accessor = new FakeActiveTeamAccessor(active: null);
        var check = new LocalNodeHealthCheck(accessor);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Returns_Degraded_when_active_team_but_gossip_not_started()
    {
        var gossip = new FakeGossipDaemon { IsRunning = false };
        var team = BuildTeam(gossip);
        var accessor = new FakeActiveTeamAccessor(active: team);
        var check = new LocalNodeHealthCheck(accessor);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Returns_Healthy_when_active_team_and_gossip_started()
    {
        var gossip = new FakeGossipDaemon { IsRunning = true };
        var team = BuildTeam(gossip);
        var accessor = new FakeActiveTeamAccessor(active: team);
        var check = new LocalNodeHealthCheck(accessor);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static TeamContext BuildTeam(IGossipDaemon? gossip)
    {
        var services = new ServiceCollection();
        if (gossip is not null)
        {
            services.AddSingleton(gossip);
        }
        return new TeamContext(TestTeamId, "Test Team", services.BuildServiceProvider());
    }

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        public FakeActiveTeamAccessor(TeamContext? active) => Active = active;
        public TeamContext? Active { get; }
        public Task SetActiveAsync(TeamId teamId, CancellationToken ct) => Task.CompletedTask;
        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;
        // Silence unused warning — event is part of the interface contract.
        private void _keep() => ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(null, null));
    }

    private sealed class FakeGossipDaemon : IGossipDaemon
    {
        public bool IsRunning { get; set; }
        public IReadOnlyCollection<PeerInfo> KnownPeers => Array.Empty<PeerInfo>();
        public event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted;

        public Task StartAsync(CancellationToken ct)
        {
            IsRunning = true;
            RoundCompleted?.Invoke(this, new GossipRoundCompletedEventArgs(0, 0, 0));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public void AddPeer(string peerEndpoint, byte[] peerPublicKey) { }
        public void RemovePeer(string peerEndpoint) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

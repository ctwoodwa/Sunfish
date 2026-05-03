using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Federation.Common;
using Sunfish.Federation.Common.Extensions;
using Sunfish.Federation.Common.Kubo;
using Xunit;

namespace Sunfish.Federation.Common.Tests;

public class FederationStartupChecksTests
{
    private static FederationStartupChecks MakeChecks(FederationOptions opts, IKuboHealthProbe? probe = null)
    {
        return new FederationStartupChecks(
            Options.Create(opts),
            NullLogger<FederationStartupChecks>.Instance,
            probe);
    }

    [Fact]
    public async Task StartAsync_InDevelopment_CompletesWithoutChecks()
    {
        var checks = MakeChecks(new FederationOptions { Environment = FederationEnvironment.Development });

        // Should not throw even without a swarm key or Kubo probe.
        await checks.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_InProduction_WithoutSwarmKey_Throws()
    {
        var checks = MakeChecks(new FederationOptions { Environment = FederationEnvironment.Production });

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await checks.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_InProduction_WithSwarmKey_NoKuboProbe_LogsWarningAndReturns()
    {
        var checks = MakeChecks(new FederationOptions
        {
            Environment = FederationEnvironment.Production,
            SwarmKeyPath = "/etc/sunfish/swarm.key",
        });

        // No probe registered — should warn and return without throwing.
        await checks.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_InProduction_WithSwarmKey_KuboPublicProfile_Throws()
    {
        var probe = Substitute.For<IKuboHealthProbe>();
        probe.GetConfigAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new KuboNetworkInfo("public", "v0.28.0")));

        var checks = MakeChecks(
            new FederationOptions
            {
                Environment = FederationEnvironment.Production,
                SwarmKeyPath = "/etc/sunfish/swarm.key",
                KuboRpcAddress = new Uri("http://127.0.0.1:5001"),
            },
            probe);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await checks.StartAsync(CancellationToken.None));
    }
}

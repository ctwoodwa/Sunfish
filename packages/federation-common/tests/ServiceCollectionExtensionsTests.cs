using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Federation.Common;
using Sunfish.Federation.Common.Extensions;
using Xunit;

namespace Sunfish.Federation.Common.Tests;

public class ServiceCollectionExtensionsTests
{
    private static IServiceCollection MakeServices()
    {
        var services = new ServiceCollection();
        // Provide ILogger<T> without a full AddLogging() dependency chain — hosting resolution of
        // FederationStartupChecks only needs ILogger<FederationStartupChecks> to satisfy DI.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    [Fact]
    public void AddSunfishFederation_RegistersHostedService()
    {
        var services = MakeServices();
        services.AddSunfishFederation(_ => { });
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>();

        Assert.Contains(hostedServices, s => s is FederationStartupChecks);
    }

    [Fact]
    public void AddSunfishFederation_RegistersPeerRegistryAndInMemoryTransport()
    {
        var services = MakeServices();
        services.AddSunfishFederation(_ => { });
        var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IPeerRegistry>();
        var transport = provider.GetRequiredService<ISyncTransport>();

        Assert.IsType<InMemoryPeerRegistry>(registry);
        Assert.IsType<InMemorySyncTransport>(transport);
    }
}

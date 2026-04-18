using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Sunfish.Bridge.Tests.Integration;

public class HealthCheckTests
{
    // Skipped by default — requires a container runtime (Podman/Docker) on the host.
    // Remove the Skip property when running locally with Podman available.
    [Fact(Skip = "Requires Podman/Docker runtime; enable locally to run.")]
    public async Task Bridge_web_responds_to_health_endpoint()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Sunfish_Bridge_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var http = app.CreateHttpClient("bridge-web");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("bridge-web");

        var response = await http.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);
    }
}

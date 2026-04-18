using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Marilo.PmDemo.Tests.Integration;

public class HealthCheckTests
{
    // Skipped by default — requires a container runtime (Podman/Docker) on the host.
    // Remove the Skip property when running locally with Podman available.
    [Fact(Skip = "Requires Podman/Docker runtime; enable locally to run.")]
    public async Task PmDemo_web_responds_to_health_endpoint()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Marilo_PmDemo_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var http = app.CreateHttpClient("pmdemo-web");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("pmdemo-web");

        var response = await http.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);
    }
}

using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Sunfish.Bridge.Tests.Integration;

public class HealthCheckTests
{
    // TRIAGE 2026-04-26 (skipped-test inventory): KEEP-SKIPPED (environmental, intentional).
    // Aspire DistributedApplicationTestingBuilder spins up a real container runtime and
    // is incompatible with the headless CI agent (no Docker-in-Docker). Local runs only.
    // Unblocker: N/A — intentional environmental gate. Re-enable on dev box with Podman/Docker.
    // See waves/cleanup/2026-04-26-followup-debt-audit.md §1d + §9 ("DO NOT TOUCH" list).
    [Fact(Skip = "KEEP-SKIPPED (environmental): requires Podman/Docker runtime on host. " +
        "Enable locally to run; intentional CI exclusion. See audit §1d + §9.")]
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

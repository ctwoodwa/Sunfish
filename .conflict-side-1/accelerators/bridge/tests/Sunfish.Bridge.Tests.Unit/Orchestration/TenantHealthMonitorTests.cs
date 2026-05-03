using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Orchestration;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for the Wave 5.2.D <see cref="TenantHealthMonitor"/>. Drives
/// the monitor through <see cref="TenantHealthMonitor.PollOnceAsync"/> with
/// a scripted <see cref="HttpMessageHandler"/> so each poll's response is
/// deterministic without a real HTTP server.
/// </summary>
/// <remarks>
/// Per Wave 5.2.D test plan: Healthy resets strikes; non-OK increments;
/// three consecutive failures emits Unhealthy; timeout counts as failure;
/// Healthy→failure→Healthy→failure→... state-machine sanity; unknown-tenant
/// polls are no-ops.
/// </remarks>
public class TenantHealthMonitorTests
{
    private static readonly Guid Tenant = new("11111111-2222-3333-4444-555555555555");
    private static readonly Uri TenantEndpoint = new("http://127.0.0.1:51001/");

    [Fact]
    public async Task Healthy_response_resets_strikes()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var (monitor, events) = BuildMonitor(client);

        // Drive two failures (strikes = 2, below threshold 3), then a success.
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(HealthyResponse);

        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        // Below threshold + subsequent success → no Unhealthy event fired, and
        // current status is Healthy.
        Assert.Equal(TenantHealthStatus.Healthy, monitor.CurrentStatuses[Tenant]);
        Assert.Empty(events);
    }

    [Fact]
    public async Task Unhealthy_response_increments_strikes()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var (monitor, events) = BuildMonitor(client);

        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadGateway));

        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        // Two failures — below threshold 3 — no Unhealthy transition yet.
        Assert.Equal(TenantHealthStatus.Unknown, monitor.CurrentStatuses[Tenant]);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ThreeConsecutiveFailures_triggers_Unhealthy_event()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var (monitor, events) = BuildMonitor(client);

        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        Assert.Equal(TenantHealthStatus.Unhealthy, monitor.CurrentStatuses[Tenant]);
        var unhealthy = Assert.Single(events);
        Assert.Equal(Tenant, unhealthy.TenantId);
        Assert.Equal(TenantHealthStatus.Unhealthy, unhealthy.Current);
    }

    [Fact]
    public async Task Timeout_counts_as_failure()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var (monitor, events) = BuildMonitor(client);

        handler.Enqueue(() => throw new TaskCanceledException("timeout"));
        handler.Enqueue(() => throw new TaskCanceledException("timeout"));
        handler.Enqueue(() => throw new TaskCanceledException("timeout"));

        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        Assert.Equal(TenantHealthStatus.Unhealthy, monitor.CurrentStatuses[Tenant]);
        var e = Assert.Single(events);
        Assert.Equal(TenantHealthStatus.Unhealthy, e.Current);
    }

    [Fact]
    public async Task FifthPoll_clears_strike_after_three_failures_and_one_success_and_another_failure()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var (monitor, events) = BuildMonitor(client);

        // Poll 1-3: fail thrice → Unhealthy event #1.
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        // Poll 4: success → Healthy event #2 (Unhealthy → Healthy).
        handler.Enqueue(HealthyResponse);
        // Poll 5: fail once → still Healthy (strike count = 1, below threshold).
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        for (var i = 0; i < 5; i++)
        {
            await monitor.PollOnceAsync(CancellationToken.None);
        }

        // After the sequence we fired exactly two transition events.
        Assert.Equal(2, events.Count);
        Assert.Equal(TenantHealthStatus.Unhealthy, events[0].Current);
        Assert.Equal(TenantHealthStatus.Healthy, events[1].Current);
        // Final state: one strike against Healthy → still Healthy.
        Assert.Equal(TenantHealthStatus.Healthy, monitor.CurrentStatuses[Tenant]);
    }

    [Fact]
    public async Task No_strikes_counted_while_tenant_unregistered()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };

        // Build a monitor whose endpoint registry is EMPTY. Nothing to poll.
        var endpoints = new InMemoryTenantEndpointRegistry();
        var options = Options.Create(new BridgeOrchestrationOptions
        {
            TenantDataRoot = "unused",
            HealthPollInterval = TimeSpan.FromMilliseconds(100),
            HealthFailureStrikeCount = 3,
        });
        var services = new ServiceCollection().BuildServiceProvider();
        var monitor = new TenantHealthMonitor(services, endpoints, options, client);

        // Polling with no endpoints registered must be a no-op — no HTTP
        // call, no state changes, no exception.
        await monitor.PollOnceAsync(CancellationToken.None);

        Assert.Empty(monitor.CurrentStatuses);
        Assert.Equal(0, handler.CallCount);
    }

    // ---- Helpers --------------------------------------------------------

    private static (TenantHealthMonitor monitor, List<TenantHealthEvent> events) BuildMonitor(HttpClient client)
    {
        var endpoints = new InMemoryTenantEndpointRegistry();
        endpoints.Register(Tenant, TenantEndpoint);

        var options = Options.Create(new BridgeOrchestrationOptions
        {
            TenantDataRoot = "unused",
            HealthPollInterval = TimeSpan.FromMilliseconds(100),
            HealthFailureStrikeCount = 3,
        });

        // Empty service provider — monitor falls back to endpoint-snapshot keys
        // when ITenantRegistry isn't registered.
        var services = new ServiceCollection().BuildServiceProvider();
        var monitor = new TenantHealthMonitor(services, endpoints, options, client);

        var events = new List<TenantHealthEvent>();
        monitor.HealthChanged += (_, e) => events.Add(e);
        return (monitor, events);
    }

    private static HttpResponseMessage HealthyResponse()
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent("Healthy"),
        };

    /// <summary>
    /// Simple scripted <see cref="HttpMessageHandler"/> that dequeues a
    /// response-producer per call. Lets tests pre-load expected responses
    /// and exceptions without bringing in Moq.Contrib.HttpClient.
    /// </summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        public int CallCount { get; private set; }

        public void Enqueue(Func<HttpResponseMessage> producer) => _responses.Enqueue(producer);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No scripted response for this call.");
            }
            var producer = _responses.Dequeue();
            try
            {
                return Task.FromResult(producer());
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}

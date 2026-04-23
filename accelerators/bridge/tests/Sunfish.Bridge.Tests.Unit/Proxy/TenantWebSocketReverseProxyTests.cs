using System.Net.WebSockets;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Middleware;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Proxy;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Proxy;

/// <summary>
/// Wave 5.3.C coverage for <see cref="TenantWebSocketReverseProxy"/>. The
/// byte-pump is exercised with a loopback Kestrel host that upgrades WS
/// connections on each side (browser + tenant), and a pair of
/// <see cref="ClientWebSocket"/>s drive traffic through
/// <see cref="TenantWebSocketReverseProxy.PumpAsync"/>. The middleware-order
/// and registry-lookup paths use synthetic <see cref="HttpContext"/>s with a
/// controllable <see cref="IHttpWebSocketFeature"/>.
/// </summary>
public class TenantWebSocketReverseProxyTests : IAsyncDisposable
{
    private static readonly Guid UnitTenant = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private WebApplication? _app;
    private Uri? _browserWsUri;
    private Uri? _tenantWsUri;

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _app.StopAsync(stopCts.Token);
            }
            catch { }
            await ((IAsyncDisposable)_app).DisposeAsync();
        }
    }

    /// <summary>
    /// Spin up a Kestrel host that exposes two upgrade endpoints:
    /// <c>/browser</c> and <c>/tenant</c>. Each accepts one WebSocket and
    /// publishes it to a promise so the test can feed the pair into
    /// <see cref="TenantWebSocketReverseProxy.PumpAsync"/>.
    /// </summary>
    private async Task<(Task<WebSocket> browserServer, Task<WebSocket> tenantServer)> StartPumpHarness()
    {
        var browserTcs = new TaskCompletionSource<WebSocket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var tenantTcs = new TaskCompletionSource<WebSocket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var closeSignal = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/browser", async (HttpContext ctx) =>
        {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            browserTcs.TrySetResult(ws);
            await closeSignal.Task; // keep request alive for the duration of the test
        });
        app.Map("/tenant", async (HttpContext ctx) =>
        {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            tenantTcs.TrySetResult(ws);
            await closeSignal.Task;
        });
        await app.StartAsync();
        _app = app;

        var httpUrl = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();
        var wsRoot = httpUrl.Replace("http://", "ws://", StringComparison.Ordinal);
        _browserWsUri = new Uri($"{wsRoot}/browser");
        _tenantWsUri = new Uri($"{wsRoot}/tenant");
        _closeSignal = closeSignal;
        return (browserTcs.Task, tenantTcs.Task);
    }

    private TaskCompletionSource<object?>? _closeSignal;

    [Fact]
    public async Task Browser_to_tenant_bytes_forward()
    {
        var (browserServerTask, tenantServerTask) = await StartPumpHarness();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var browserClient = new ClientWebSocket();
        using var tenantClient = new ClientWebSocket();
        await browserClient.ConnectAsync(_browserWsUri!, cts.Token);
        await tenantClient.ConnectAsync(_tenantWsUri!, cts.Token);

        var browserServer = await browserServerTask;
        var tenantServer = await tenantServerTask;

        var pumpTask = TenantWebSocketReverseProxy.PumpAsync(
            browserServer, tenantClient, NullLogger.Instance, cts.Token);

        var payload = Encoding.UTF8.GetBytes("hello-from-browser");
        await browserClient.SendAsync(
            new ArraySegment<byte>(payload),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cts.Token);

        var buf = new byte[256];
        var result = await tenantServer.ReceiveAsync(buf, cts.Token);
        Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
        Assert.Equal(payload, buf.AsSpan(0, result.Count).ToArray());

        // Trigger pump shutdown by closing browser output.
        await browserClient.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
        await pumpTask;
    }

    [Fact]
    public async Task Tenant_to_browser_bytes_forward()
    {
        var (browserServerTask, tenantServerTask) = await StartPumpHarness();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var browserClient = new ClientWebSocket();
        using var tenantClient = new ClientWebSocket();
        await browserClient.ConnectAsync(_browserWsUri!, cts.Token);
        await tenantClient.ConnectAsync(_tenantWsUri!, cts.Token);

        var browserServer = await browserServerTask;
        var tenantServer = await tenantServerTask;

        var pumpTask = TenantWebSocketReverseProxy.PumpAsync(
            browserServer, tenantClient, NullLogger.Instance, cts.Token);

        var payload = Encoding.UTF8.GetBytes("hello-from-tenant");
        await tenantServer.SendAsync(
            new ArraySegment<byte>(payload),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cts.Token);

        var buf = new byte[256];
        var result = await browserClient.ReceiveAsync(buf, cts.Token);
        Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
        Assert.Equal(payload, buf.AsSpan(0, result.Count).ToArray());

        await tenantServer.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
        await pumpTask;
    }

    [Fact]
    public async Task Browser_close_propagates_to_tenant()
    {
        var (browserServerTask, tenantServerTask) = await StartPumpHarness();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var browserClient = new ClientWebSocket();
        using var tenantClient = new ClientWebSocket();
        await browserClient.ConnectAsync(_browserWsUri!, cts.Token);
        await tenantClient.ConnectAsync(_tenantWsUri!, cts.Token);

        var browserServer = await browserServerTask;
        var tenantServer = await tenantServerTask;

        var pumpTask = TenantWebSocketReverseProxy.PumpAsync(
            browserServer, tenantClient, NullLogger.Instance, cts.Token);

        // Browser initiates a close. The pump receives it on browserServer and
        // forwards the close intent to tenantClient, which delivers it to
        // tenantServer (the far side of the tenant connection).
        await browserClient.CloseOutputAsync(
            WebSocketCloseStatus.NormalClosure, "browser-bye", cts.Token);

        var buf = new byte[256];
        var result = await tenantServer.ReceiveAsync(buf, cts.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, result.CloseStatus);
        await pumpTask;
    }

    [Fact]
    public async Task Tenant_close_propagates_to_browser()
    {
        var (browserServerTask, tenantServerTask) = await StartPumpHarness();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var browserClient = new ClientWebSocket();
        using var tenantClient = new ClientWebSocket();
        await browserClient.ConnectAsync(_browserWsUri!, cts.Token);
        await tenantClient.ConnectAsync(_tenantWsUri!, cts.Token);

        var browserServer = await browserServerTask;
        var tenantServer = await tenantServerTask;

        var pumpTask = TenantWebSocketReverseProxy.PumpAsync(
            browserServer, tenantClient, NullLogger.Instance, cts.Token);

        // Tenant-side initiates a close (simulating the tenant child's
        // /ws handler closing the socket). The pump receives on tenantClient
        // and forwards to browserServer, which delivers to browserClient.
        await tenantServer.CloseOutputAsync(
            WebSocketCloseStatus.NormalClosure, "tenant-bye", cts.Token);

        var buf = new byte[256];
        var result = await browserClient.ReceiveAsync(buf, cts.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, result.CloseStatus);
        await pumpTask;
    }

    [Fact]
    public async Task Unknown_tenant_returns_404()
    {
        var ctx = BuildHttpContext(
            tenantContext: BindContext(new FakeBrowserTenantContext(), tenantId: UnitTenant),
            endpointRegistry: new InMemoryTenantEndpointRegistry(),   // empty
            wsFactory: null,
            isWebSocketRequest: false);

        await TenantWebSocketReverseProxy.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Missing_IBrowserTenantContext_returns_500()
    {
        var ctx = BuildHttpContext(
            tenantContext: null,
            endpointRegistry: new InMemoryTenantEndpointRegistry(),
            wsFactory: null,
            isWebSocketRequest: false);

        await TenantWebSocketReverseProxy.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
    }

    // -----------------------------------------------------------------
    // Harness
    // -----------------------------------------------------------------

    private static IBrowserTenantContext BindContext(
        FakeBrowserTenantContext ctx, Guid tenantId)
    {
        ctx.Bind(tenantId, "unit", TrustLevel.RelayOnly, null, new byte[16]);
        return ctx;
    }

    private static HttpContext BuildHttpContext(
        IBrowserTenantContext? tenantContext,
        ITenantEndpointRegistry endpointRegistry,
        IUpstreamWebSocketFactory? wsFactory,
        bool isWebSocketRequest)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (tenantContext is not null)
        {
            services.AddSingleton(tenantContext);
        }
        services.AddSingleton(endpointRegistry);
        if (wsFactory is not null)
        {
            services.AddSingleton(wsFactory);
        }
        var sp = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext
        {
            RequestServices = sp,
        };
        ctx.Features.Set<IHttpWebSocketFeature>(new FakeWsFeature(isWebSocketRequest));
        return ctx;
    }

    private sealed class FakeWsFeature : IHttpWebSocketFeature
    {
        public FakeWsFeature(bool isWebSocketRequest)
        {
            IsWebSocketRequest = isWebSocketRequest;
        }

        public bool IsWebSocketRequest { get; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) =>
            throw new NotSupportedException(
                "TenantWebSocketReverseProxyTests unit harness should not reach AcceptAsync.");
    }

    private sealed class FakeBrowserTenantContext : IBrowserTenantContext
    {
        public bool IsResolved { get; private set; }
        public Guid TenantId { get; private set; }
        public string Slug { get; private set; } = string.Empty;
        public TrustLevel TrustLevel { get; private set; }
        public byte[]? TeamPublicKey { get; private set; }
        public byte[] AuthSalt { get; private set; } = Array.Empty<byte>();

        public void Bind(Guid tenantId, string slug, TrustLevel trustLevel, byte[]? teamPublicKey, byte[] authSalt)
        {
            IsResolved = true;
            TenantId = tenantId;
            Slug = slug;
            TrustLevel = trustLevel;
            TeamPublicKey = teamPublicKey;
            AuthSalt = authSalt;
        }
    }
}

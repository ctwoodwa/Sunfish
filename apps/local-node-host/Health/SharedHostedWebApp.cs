using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Net.WebSockets;

namespace Sunfish.LocalNodeHost.Health;

/// <summary>
/// Wave 5.3.C shared Kestrel-backed <see cref="WebApplication"/> that hosts
/// every in-process HTTP surface owned by <c>local-node-host</c>. Sibling
/// hosted services (<see cref="HostedHealthEndpoint"/>, <see cref="HostedWebSocketEndpoint"/>)
/// register their paths here during their own <see cref="IHostedService.StartAsync"/>,
/// then this service starts the underlying <see cref="WebApplication"/> last
/// (by virtue of being registered after them in the composition root).
/// </summary>
/// <remarks>
/// <para>
/// Before Wave 5.3.C the <see cref="HostedHealthEndpoint"/> owned its own
/// <see cref="WebApplication"/>. Wave 5.3.C adds a second HTTP surface
/// (<c>/ws</c> for the WebSocket sync-daemon transport) that must share the
/// same Kestrel listener: two <c>WebApplication</c> instances cannot both bind
/// to <c>ASPNETCORE_URLS</c>, and per-port multiplexing would double the
/// attack surface and confuse Bridge's <c>TenantEndpointRegistry</c> (which
/// maps one tenant-child to one URI). The refactor extracts the lifecycle
/// into this singleton; both endpoint services become thin path registrars.
/// </para>
/// <para>
/// <b>Lifecycle.</b>
/// <list type="number">
///   <item>Constructor: build the <see cref="WebApplication"/>, call
///     <see cref="WebSocketMiddlewareExtensions.UseWebSockets(IApplicationBuilder)"/>.</item>
///   <item>Sibling services' <c>StartAsync</c>: invoke
///     <see cref="MapHealthCheckIfAbsent"/> or <see cref="MapWebSocketPath"/>
///     to register their paths. Calls after <see cref="StartAsync"/> throw.</item>
///   <item>Own <c>StartAsync</c>: call <c>_app.StartAsync</c> so Kestrel binds
///     and the registered endpoints go live. Captures <see cref="SelectedUrl"/>
///     for downstream tests + the Bridge supervisor.</item>
///   <item>Own <c>StopAsync</c>: graceful Kestrel shutdown.</item>
///   <item><see cref="IAsyncDisposable"/>: disposes the <see cref="WebApplication"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Port selection.</b> Unchanged from Wave 5.2.D: <c>ASPNETCORE_URLS</c>
/// wins when present (Aspire / Bridge supervisor inject it); otherwise
/// <see cref="LocalNodeOptions.HealthPort"/> is honoured; <c>0</c> delegates
/// to Kestrel's ephemeral-port selection.
/// </para>
/// </remarks>
public sealed class SharedHostedWebApp : IHostedService, IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ILogger<SharedHostedWebApp> _logger;
    private readonly LocalNodeOptions _options;

    private bool _started;
    private bool _healthMapped;

    /// <summary>
    /// URL Kestrel actually bound to — stable after <see cref="StartAsync"/>
    /// completes. <see langword="null"/> before start. Exposed primarily for
    /// in-process tests that need to know the auto-assigned port, and for
    /// <see cref="HostedHealthEndpoint"/>'s <c>SelectedUrl</c> passthrough.
    /// </summary>
    public string? SelectedUrl { get; private set; }

    /// <summary>
    /// Exposed for the sibling hosted services so they can resolve scoped
    /// services (e.g. <see cref="IHealthCheck"/>) against the inner container
    /// without touching the outer root provider directly.
    /// </summary>
    public IServiceProvider InnerServices => _app.Services;

    /// <summary>
    /// Construct the shared app. Builds the underlying <see cref="WebApplication"/>
    /// but does NOT start Kestrel — that happens in <see cref="StartAsync"/>.
    /// </summary>
    public SharedHostedWebApp(
        IServiceProvider outerServices,
        IOptions<LocalNodeOptions> options,
        ILogger<SharedHostedWebApp> logger)
    {
        ArgumentNullException.ThrowIfNull(outerServices);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Suppress content-root inference: we share the outer host's cwd
            // but are not serving files from it.
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Bridge the outer container's IActiveTeamAccessor into the inner
        // container so AddCheck<LocalNodeHealthCheck> can resolve it when
        // the health endpoint is mapped.
        builder.Services.AddSingleton(sp =>
            outerServices.GetRequiredService<Sunfish.Kernel.Runtime.Teams.IActiveTeamAccessor>());
        builder.Services.AddHealthChecks()
            .AddCheck<LocalNodeHealthCheck>("local-node");

        ConfigureUrls(builder.WebHost);

        _app = builder.Build();
        _app.UseWebSockets();
    }

    /// <summary>
    /// Register the <c>/health</c> mapping if no sibling has done so already.
    /// Idempotent. Called by <see cref="HostedHealthEndpoint.StartAsync"/>.
    /// Must be called before <see cref="StartAsync"/>.
    /// </summary>
    public void MapHealthCheckIfAbsent()
    {
        ObjectDisposedException.ThrowIf(_started, this);
        if (_healthMapped)
        {
            return;
        }
        _app.MapHealthChecks("/health");
        _healthMapped = true;
    }

    /// <summary>
    /// Register a WebSocket upgrade handler for <paramref name="path"/>. The
    /// <paramref name="onAccepted"/> callback is invoked once the upgrade
    /// completes, receiving the raw <see cref="WebSocket"/> handle. The HTTP
    /// request lifetime is tied to the callback's completion — return from
    /// <paramref name="onAccepted"/> only after the WebSocket session is done.
    /// </summary>
    /// <remarks>
    /// Non-WebSocket requests to <paramref name="path"/> respond with
    /// HTTP 400. Must be called before <see cref="StartAsync"/>.
    /// </remarks>
    public void MapWebSocketPath(string path, Func<WebSocket, CancellationToken, Task> onAccepted)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(onAccepted);
        ObjectDisposedException.ThrowIf(_started, this);

        _app.Map(path, async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await onAccepted(ws, ctx.RequestAborted).ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }
        _started = true;

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        var serverAddresses = _app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        SelectedUrl = serverAddresses?.Addresses?.FirstOrDefault();

        _logger.LogInformation(
            "Wave 5.3.C shared-hosted-web-app bound at {Url} (LocalNode:HealthPort={HealthPort}); " +
            "health-mapped={HealthMapped}",
            SelectedUrl ?? "(unknown)",
            _options.HealthPort,
            _healthMapped);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        try
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the outer host's shutdown token fires before
            // Kestrel's graceful-stop completes. Not an error condition.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ((IAsyncDisposable)_app).DisposeAsync().ConfigureAwait(false);
    }

    private void ConfigureUrls(IWebHostBuilder webHost)
    {
        // ASPNETCORE_URLS wins when present — Aspire and the Bridge supervisor
        // inject it. Otherwise honour LocalNode:HealthPort; if it is 0 (the
        // default), Kestrel picks an ephemeral port.
        var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (!string.IsNullOrWhiteSpace(envUrls))
        {
            // Respect env-injected URLs — do not override.
            return;
        }

        var port = _options.HealthPort;
        webHost.UseUrls($"http://127.0.0.1:{port}");
    }
}

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

namespace Sunfish.LocalNodeHost.Health;

/// <summary>
/// Wave 5.2.D <see cref="IHostedService"/> that embeds a Kestrel-backed
/// minimal <see cref="WebApplication"/> inside the Worker-SDK composition root
/// and exposes a single <c>GET /health</c> endpoint bound to the
/// <see cref="LocalNodeHealthCheck"/>. Keeps the main host project on
/// <c>Microsoft.NET.Sdk.Worker</c> (a <see cref="FrameworkReference"/> to
/// <c>Microsoft.AspNetCore.App</c> pulls Kestrel + the health-check middleware
/// in without switching SDKs).
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately NOT a second <see cref="IHost"/>: we compose a bare
/// <see cref="WebApplication"/> via <see cref="WebApplication.CreateBuilder(WebApplicationOptions)"/>
/// and drive its lifecycle from <see cref="StartAsync"/> /
/// <see cref="StopAsync"/>. The inner app gets its own DI container, so we
/// forward the outer host's <see cref="IHealthCheck"/> registrations into the
/// inner container by re-registering the <see cref="LocalNodeHealthCheck"/>
/// against the outer provider's <c>IActiveTeamAccessor</c>.
/// </para>
/// <para>
/// <b>Port selection.</b> The URL is determined by, in order of precedence:
/// <list type="number">
///   <item>The <c>ASPNETCORE_URLS</c> environment variable (Aspire / Bridge
///     supervisor inject this per child).</item>
///   <item><see cref="LocalNodeOptions.HealthPort"/> when non-zero.</item>
///   <item>Port 0 — Kestrel auto-assigns an ephemeral port.</item>
/// </list>
/// The resolved port is logged on startup so the outer supervisor can
/// correlate logs; when auto-assigned it is surfaced via
/// <see cref="SelectedUrl"/> for in-process test consumers.
/// </para>
/// </remarks>
public sealed class HostedHealthEndpoint : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _outerServices;
    private readonly LocalNodeOptions _options;
    private readonly ILogger<HostedHealthEndpoint> _logger;
    private WebApplication? _app;

    /// <summary>
    /// URL Kestrel actually bound to — stable after <see cref="StartAsync"/>
    /// completes. <see langword="null"/> before start. Exposed primarily for
    /// in-process tests that need to know the auto-assigned port.
    /// </summary>
    public string? SelectedUrl { get; private set; }

    /// <summary>
    /// Construct the hosted endpoint. The outer DI container is captured so
    /// the inner <see cref="WebApplication"/>'s health check can resolve the
    /// install-level <see cref="Sunfish.Kernel.Runtime.Teams.IActiveTeamAccessor"/>.
    /// </summary>
    public HostedHealthEndpoint(
        IServiceProvider outerServices,
        IOptions<LocalNodeOptions> options,
        ILogger<HostedHealthEndpoint> logger)
    {
        ArgumentNullException.ThrowIfNull(outerServices);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _outerServices = outerServices;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Suppress content-root inference: we share the outer host's cwd
            // but are not serving files from it.
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Resolve the health target from the outer container. The inner
        // container has its own IServiceProvider; we wire an adapter so
        // AddCheck<LocalNodeHealthCheck> can resolve IActiveTeamAccessor
        // against the outer composition root (where MultiTeamBootstrap +
        // ActiveTeamAccessor live).
        builder.Services.AddSingleton(sp =>
            _outerServices.GetRequiredService<Sunfish.Kernel.Runtime.Teams.IActiveTeamAccessor>());
        builder.Services.AddHealthChecks()
            .AddCheck<LocalNodeHealthCheck>("local-node");

        ConfigureUrls(builder.WebHost);

        _app = builder.Build();
        _app.MapHealthChecks("/health");

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        // Capture the resolved URL so tests / supervisors can dial it without
        // having to re-read IConfiguration. ServerAddressesFeature is the
        // canonical post-start port-discovery API.
        var serverAddresses = _app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        SelectedUrl = serverAddresses?.Addresses?.FirstOrDefault();

        _logger.LogInformation(
            "Wave 5.2.D health endpoint bound at {Url} (LocalNode:HealthPort={HealthPort})",
            SelectedUrl ?? "(unknown)",
            _options.HealthPort);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
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
        if (_app is not null)
        {
            await ((IAsyncDisposable)_app).DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
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

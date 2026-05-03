using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sunfish.LocalNodeHost.Health;

/// <summary>
/// Wave 5.2.D <see cref="IHostedService"/> that exposes the <c>/health</c>
/// endpoint. Wave 5.3.C refactor: the endpoint no longer owns its own
/// <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>; instead it
/// registers the <c>/health</c> mapping on the singleton
/// <see cref="SharedHostedWebApp"/>, which owns the shared Kestrel lifecycle
/// so <c>/health</c> and <c>/ws</c> live behind the same listener.
/// </summary>
/// <remarks>
/// <para>
/// The hosted service must be registered BEFORE <see cref="SharedHostedWebApp"/>
/// in the composition root so its <see cref="StartAsync"/> runs first and the
/// path registration happens while the shared app is still in its
/// pre-<c>StartAsync</c> configuration phase. The shared app's own
/// <c>StartAsync</c> runs last and starts Kestrel after every path has been
/// mapped.
/// </para>
/// <para>
/// <b>Port selection.</b> Unchanged from Wave 5.2.D — <c>ASPNETCORE_URLS</c>
/// &gt; <see cref="LocalNodeOptions.HealthPort"/> &gt; ephemeral. The URL is
/// still surfaced post-start via <see cref="SelectedUrl"/> (delegates to
/// <see cref="SharedHostedWebApp.SelectedUrl"/>).
/// </para>
/// </remarks>
public sealed class HostedHealthEndpoint : IHostedService
{
    private readonly SharedHostedWebApp _sharedApp;
    private readonly LocalNodeOptions _options;
    private readonly ILogger<HostedHealthEndpoint> _logger;

    /// <summary>
    /// URL Kestrel actually bound to — stable after
    /// <see cref="SharedHostedWebApp.StartAsync"/> completes. Delegates to
    /// the shared app so existing in-process test consumers keep working.
    /// </summary>
    public string? SelectedUrl => _sharedApp.SelectedUrl;

    /// <summary>
    /// Construct the hosted endpoint. Captures the shared web-app singleton
    /// whose lifecycle this service hooks into.
    /// </summary>
    public HostedHealthEndpoint(
        SharedHostedWebApp sharedApp,
        IOptions<LocalNodeOptions> options,
        ILogger<HostedHealthEndpoint> logger)
    {
        ArgumentNullException.ThrowIfNull(sharedApp);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _sharedApp = sharedApp;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sharedApp.MapHealthCheckIfAbsent();
        _logger.LogInformation(
            "Wave 5.2.D health endpoint registered on shared hosted web-app " +
            "(LocalNode:HealthPort={HealthPort}).",
            _options.HealthPort);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Shared app owns Kestrel shutdown.
        return Task.CompletedTask;
    }
}

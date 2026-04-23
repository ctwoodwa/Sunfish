using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.LocalNodeHost.Health;

/// <summary>
/// Wave 5.3.C hosted service that maps <c>/ws</c> on the shared Kestrel
/// listener. Every inbound WebSocket is handed to the registered
/// <see cref="ISyncDaemonAcceptor"/>, which wraps it in a
/// <see cref="Sunfish.Kernel.Sync.Protocol.WebSocketSyncDaemonTransport"/> and
/// eventually feeds it into the sync daemon's session pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Wave 5.3.C ships with a stub <see cref="LoggingSyncDaemonAcceptor"/> that
/// logs the connection and closes it cleanly. Wave 5.3.D replaces the
/// acceptor with the real session pipeline (accept → HELLO handshake →
/// DELTA_STREAM pump).
/// </para>
/// <para>
/// Controlled by <see cref="BrowserWebSocketOptions"/>: when
/// <see cref="BrowserWebSocketOptions.Enabled"/> is <c>false</c>, the path
/// is not mapped and the endpoint is effectively absent. The shared Kestrel
/// listener still serves <c>/health</c>.
/// </para>
/// </remarks>
public sealed class HostedWebSocketEndpoint : IHostedService
{
    private readonly SharedHostedWebApp _sharedApp;
    private readonly BrowserWebSocketOptions _options;
    private readonly ISyncDaemonAcceptor _acceptor;
    private readonly ILogger<HostedWebSocketEndpoint> _logger;

    /// <summary>
    /// Construct the hosted WS endpoint. All dependencies come from the outer
    /// composition root — <see cref="SharedHostedWebApp"/> is a singleton and
    /// the acceptor is DI-resolved so Wave 5.3.D can swap in the session
    /// pipeline without touching this type.
    /// </summary>
    public HostedWebSocketEndpoint(
        SharedHostedWebApp sharedApp,
        IOptions<LocalNodeOptions> options,
        ISyncDaemonAcceptor acceptor,
        ILogger<HostedWebSocketEndpoint> logger)
    {
        ArgumentNullException.ThrowIfNull(sharedApp);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(acceptor);
        ArgumentNullException.ThrowIfNull(logger);

        _sharedApp = sharedApp;
        _options = options.Value.BrowserWebSocket;
        _acceptor = acceptor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Wave 5.3.C /ws endpoint disabled via LocalNode:BrowserWebSocket:Enabled=false.");
            return Task.CompletedTask;
        }

        _sharedApp.MapWebSocketPath("/ws", async (ws, ct) =>
        {
            try
            {
                await _acceptor.AcceptAsync(ws, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Host shutting down — fine.
            }
            catch (Exception ex)
            {
                // Never let acceptor exceptions escape into the Kestrel request
                // pipeline; that would surface as a 500 on an upgraded WS which
                // is not a useful signal to the client.
                _logger.LogError(ex, "Sync-daemon acceptor threw while handling /ws connection.");
            }
        });
        _logger.LogInformation(
            "Wave 5.3.C /ws endpoint registered on shared hosted web-app " +
            "(MaxMessageBytes={MaxMessageBytes}).",
            _options.MaxMessageBytes);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Shared app owns Kestrel shutdown.
        return Task.CompletedTask;
    }
}

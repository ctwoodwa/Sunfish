using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sunfish.Bridge.Relay;

/// <summary>
/// <see cref="BackgroundService"/> wrapper that starts <see cref="IRelayServer"/>
/// when Bridge is deployed in <see cref="BridgeMode.Relay"/> posture
/// (<see href="../../docs/adrs/0026-bridge-posture.md">ADR 0026</see>).
/// </summary>
/// <remarks>
/// SaaS-posture deployments do not register this hosted service at all —
/// the composition root in <c>Program.cs</c> only wires it in when
/// <c>BridgeOptions.Mode == Relay</c>. Keeping the worker registration
/// posture-scoped avoids accidentally running the relay accept loop
/// next to the Blazor Server host.
/// </remarks>
public sealed class RelayWorker : BackgroundService
{
    private readonly IRelayServer _server;
    private readonly ILogger<RelayWorker> _logger;

    public RelayWorker(IRelayServer server, ILogger<RelayWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(logger);
        _server = server;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server.NodeConnected += OnNodeConnected;
        _server.NodeDisconnected += OnNodeDisconnected;

        try
        {
            await _server.StartAsync(stoppingToken).ConfigureAwait(false);

            // Idle until the host shuts us down. The accept loop runs on its
            // own task inside RelayServer; this worker's only job is lifecycle.
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* shutdown */ }

            await _server.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _server.NodeConnected -= OnNodeConnected;
            _server.NodeDisconnected -= OnNodeDisconnected;
        }
    }

    private void OnNodeConnected(object? sender, NodeConnectedEventArgs e)
    {
        _logger.LogInformation(
            "Relay peer connected: nodeId={NodeId} team='{Team}' remote={Remote}.",
            e.Node.NodeId, e.Node.TeamId, e.Node.RemoteEndpoint);
    }

    private void OnNodeDisconnected(object? sender, NodeDisconnectedEventArgs e)
    {
        _logger.LogInformation(
            "Relay peer disconnected: nodeId={NodeId} reason={Reason}.",
            e.NodeId, e.Reason);
    }
}

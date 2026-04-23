using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Default <see cref="INodeHost"/>. Thin shell for Wave 1.1 — state machine
/// plus plugin-registry passthrough. Sync-daemon orchestration, projection
/// scheduling, and stream topology wiring land in Waves 1.2 / 1.3 / 2.1.
/// </summary>
public sealed class NodeHost : INodeHost
{
    private readonly ILogger<NodeHost> _logger;
    private readonly object _gate = new();

    /// <summary>Create a host backed by <paramref name="plugins"/>.</summary>
    /// <param name="plugins">The plugin registry this host exposes.</param>
    /// <param name="logger">Optional logger. Falls back to <see cref="NullLogger{T}"/> when null.</param>
    public NodeHost(IPluginRegistry plugins, ILogger<NodeHost>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        Plugins = plugins;
        _logger = logger ?? NullLogger<NodeHost>.Instance;
    }

    /// <inheritdoc />
    public NodeState State { get; private set; } = NodeState.Stopped;

    /// <inheritdoc />
    public IPluginRegistry Plugins { get; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (State == NodeState.Running || State == NodeState.Starting)
            {
                throw new InvalidOperationException(
                    $"Cannot start NodeHost: current state is {State}.");
            }
            if (State == NodeState.Faulted)
            {
                throw new InvalidOperationException(
                    "Cannot start NodeHost: host is Faulted. Create a new host instance.");
            }

            State = NodeState.Starting;
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogDebug("NodeHost starting");
            // Wave 1.1 scope: no additional start-up work. Plugin loading is
            // driven by callers so tests and composition roots can control
            // ordering relative to other services (event bus, schema registry,
            // etc.). Wave 1.2/2.x grows this out.
            lock (_gate)
            {
                State = NodeState.Running;
            }
            return Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            lock (_gate) { State = NodeState.Stopped; }
            throw;
        }
        catch
        {
            lock (_gate) { State = NodeState.Faulted; }
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (State == NodeState.Stopped)
            {
                return Task.CompletedTask;
            }
            if (State == NodeState.Stopping)
            {
                throw new InvalidOperationException(
                    "Cannot stop NodeHost: a stop is already in progress.");
            }

            State = NodeState.Stopping;
        }

        try
        {
            _logger.LogDebug("NodeHost stopping");
            // Wave 1.1 scope: symmetric with Start — no additional shutdown
            // work. Plugin unload is a caller-driven step; tests that exercise
            // plugin teardown call Plugins.UnloadAllAsync explicitly.
            lock (_gate)
            {
                State = NodeState.Stopped;
            }
            return Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            lock (_gate) { State = NodeState.Faulted; }
            throw;
        }
        catch
        {
            lock (_gate) { State = NodeState.Faulted; }
            throw;
        }
    }
}

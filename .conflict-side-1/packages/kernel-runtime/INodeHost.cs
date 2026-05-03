namespace Sunfish.Kernel.Runtime;

/// <summary>
/// The runtime surface of the local-node kernel — paper §5.1. Responsible for
/// starting and stopping the node, coordinating plugin lifecycle, and
/// (in future waves) the sync daemon, projection scheduler, and stream
/// topology. This Wave-1.1 surface is intentionally thin; it grows in
/// Waves 1.2 / 1.3 / 2.1.
/// </summary>
public interface INodeHost
{
    /// <summary>
    /// Transition <see cref="State"/> from <see cref="NodeState.Stopped"/>
    /// through <see cref="NodeState.Starting"/> to <see cref="NodeState.Running"/>.
    /// </summary>
    /// <param name="ct">Cancellation token observed during start.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Transition <see cref="State"/> from <see cref="NodeState.Running"/>
    /// through <see cref="NodeState.Stopping"/> to <see cref="NodeState.Stopped"/>.
    /// </summary>
    /// <param name="ct">Cancellation token observed during stop.</param>
    Task StopAsync(CancellationToken ct);

    /// <summary>Current lifecycle state. See <see cref="NodeState"/>.</summary>
    NodeState State { get; }

    /// <summary>Plugin registry scoped to this host.</summary>
    IPluginRegistry Plugins { get; }
}

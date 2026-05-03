namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Lifecycle states of the local-node kernel host. See
/// <see cref="INodeHost"/> for the state transitions.
/// </summary>
public enum NodeState
{
    /// <summary>The host has not been started, or has been stopped cleanly.</summary>
    Stopped,

    /// <summary>The host is executing <see cref="INodeHost.StartAsync"/>.</summary>
    Starting,

    /// <summary>The host has completed start-up and is serving plugins.</summary>
    Running,

    /// <summary>The host is executing <see cref="INodeHost.StopAsync"/>.</summary>
    Stopping,

    /// <summary>The host caught an unrecoverable error during start or stop. Diagnose via logs.</summary>
    Faulted,
}

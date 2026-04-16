namespace Sunfish.Core.Enums;

/// <summary>
/// Describes the aggregate health across all registered SignalR hub connections.
/// Used to determine the root icon state of the connection status indicator.
/// </summary>
public enum AggregateConnectionState
{
    /// <summary>All critical hubs are connected and healthy.</summary>
    Healthy,

    /// <summary>At least one critical hub is reconnecting or connecting, but none are fully disconnected.</summary>
    Degraded,

    /// <summary>At least one critical hub is disconnected.</summary>
    Offline,

    /// <summary>All critical hubs are healthy; only noncritical hubs are unhealthy.</summary>
    Partial
}

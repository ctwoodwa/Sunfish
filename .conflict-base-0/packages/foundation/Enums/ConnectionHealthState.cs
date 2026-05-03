namespace Sunfish.Foundation.Enums;

/// <summary>
/// Describes the health of an individual SignalR hub connection,
/// derived from <see cref="Microsoft.AspNetCore.SignalR.Client.HubConnectionState"/>
/// plus application-level health signals such as heartbeat staleness.
/// </summary>
public enum ConnectionHealthState
{
    /// <summary>Connected with no recent errors and a fresh heartbeat.</summary>
    Healthy,

    /// <summary>Initial connection attempt in progress.</summary>
    Connecting,

    /// <summary>Lost connection; automatic reconnect attempts are underway.</summary>
    Recovering,

    /// <summary>Connected but heartbeat is stale beyond the configured threshold.</summary>
    Degraded,

    /// <summary>Disconnected after all reconnect attempts failed.</summary>
    Offline
}

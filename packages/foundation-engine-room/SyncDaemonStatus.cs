using System.Text.Json.Serialization;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Health discriminator for the sync daemon per ADR 0079 §1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncDaemonStatus
{
    /// <summary>Daemon is operational with all peers reachable.</summary>
    Healthy,

    /// <summary>Daemon is operational but some peers are unreachable or throughput is below threshold.</summary>
    Degraded,

    /// <summary>Daemon is offline or all peers are unreachable.</summary>
    Unavailable,
}

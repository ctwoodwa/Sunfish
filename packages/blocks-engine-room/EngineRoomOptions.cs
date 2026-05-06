using System;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// Host-configurable Engine Room tunables per ADR 0079 §2 +
/// W#50 Phase 2.
/// </summary>
public sealed class EngineRoomOptions
{
    /// <summary>
    /// Cadence used by
    /// <see cref="Sunfish.Foundation.EngineRoom.IEngineRoomDataProvider.SubscribeHealthAsync"/>
    /// implementations to emit periodic health updates between status-
    /// change events. Default 30 seconds per ADR 0079 §2.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cooldown window applied per
    /// <c>(TenantId, EngineRoomSubsystem, statusFrom, statusTo)</c> tuple
    /// during health-degradation audit emission per W#50 Phase 2 dedup
    /// contract. The same tuple within the cooldown emits at most one
    /// audit record; different tuples fire independently. Default 30
    /// seconds.
    /// </summary>
    public TimeSpan DegradationDedupCooldown { get; set; } = TimeSpan.FromSeconds(30);
}

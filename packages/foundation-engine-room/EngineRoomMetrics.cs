namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// OpenTelemetry instrument names for the Engine Room observability
/// surface per ADR 0079 §3. Centralizing the constants here ensures
/// downstream consumers (Phase 2 reference impls, Bridge / Anchor host
/// metrics exporters) bind to identical strings without copy-paste drift.
/// </summary>
public static class EngineRoomMetrics
{
    /// <summary>OTel Meter name; binds to <c>Sunfish.EngineRoom</c>.</summary>
    public const string MeterName = "Sunfish.EngineRoom";

    /// <summary>OTel ActivitySource name; binds to <c>Sunfish.EngineRoom</c>.</summary>
    public const string ActivitySourceName = "Sunfish.EngineRoom";

    /// <summary>Gauge — current peer count.</summary>
    public const string PeerCount = "sunfish.engine_room.peer_count";

    /// <summary>Counter — events processed per second (moving average).</summary>
    public const string EventsThroughput = "sunfish.engine_room.events_throughput";

    /// <summary>Counter — total gossip cycles since daemon start.</summary>
    public const string GossipCycles = "sunfish.engine_room.gossip_cycles";

    /// <summary>Gauge — total CRDT bytes across all documents in tenant scope.</summary>
    public const string CrdtTotalBytes = "sunfish.engine_room.crdt_total_bytes";

    /// <summary>Gauge — count of documents currently flagged compaction-eligible.</summary>
    public const string CrdtCompactionEligible = "sunfish.engine_room.crdt_compaction_eligible";

    /// <summary>Gauge — per-subsystem status (encoded as int per <see cref="SubsystemStatus"/>).</summary>
    public const string SubsystemStatusGauge = "sunfish.engine_room.subsystem_status";
}

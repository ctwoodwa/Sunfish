using System.Text.Json.Serialization;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Engine Room subsystem discriminator per ADR 0079 §1 + §3. Each
/// subsystem maps to a panel in the Engine Room UI cohort
/// (<c>blocks-engine-room</c>); the v1 set is closed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EngineRoomSubsystem
{
    /// <summary>Sync daemon + CRDT growth — the operational baseline.</summary>
    MainPropulsion,

    /// <summary>Power / runtime resource usage; render-side panel ships in Phase 3a.</summary>
    Electrical,

    /// <summary>Quarantine / release / compaction operations; gated on permission for write actions per §Trust.</summary>
    DamageControl,

    /// <summary>Structural-integrity audits + analyzer findings; render-side panel ships in Phase 3b.</summary>
    QaWorkshop,
}

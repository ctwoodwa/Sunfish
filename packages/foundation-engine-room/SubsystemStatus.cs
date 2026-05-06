using System.Text.Json.Serialization;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Per-subsystem status discriminator per ADR 0079 §1. The Engine Room
/// dashboard renders one tile per <see cref="EngineRoomSubsystem"/>
/// colored by this value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubsystemStatus
{
    /// <summary>Subsystem is operational; all metrics within nominal bounds.</summary>
    Operational,

    /// <summary>Subsystem is operational but at least one metric is outside nominal bounds.</summary>
    Warning,

    /// <summary>Subsystem is unavailable or in an error state requiring intervention.</summary>
    Critical,

    /// <summary>Subsystem state is not yet known (e.g., daemon not yet reporting).</summary>
    Unknown,
}

using System.Collections.Generic;
using System.Linq;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Aggregate health snapshot across every <see cref="EngineRoomSubsystem"/>
/// per ADR 0079 §1 + §2. The Engine Room dashboard renders one tile per
/// entry in <see cref="SubsystemHealthList"/>.
/// </summary>
/// <param name="SubsystemHealthList">Per-subsystem entries; the list MAY omit subsystems whose providers haven't reported (UI surfaces those as <see cref="SubsystemStatus.Unknown"/>).</param>
public sealed record EngineRoomHealthSummary(
    IReadOnlyList<SubsystemHealth> SubsystemHealthList)
{
    /// <summary>
    /// Returns the entry for <paramref name="subsystem"/> or null when the
    /// list does not yet contain an entry. Callers that prefer
    /// "Unknown-on-missing" semantics can convert the null to a synthesized
    /// <see cref="SubsystemHealth"/> with <see cref="SubsystemStatus.Unknown"/>.
    /// </summary>
    public SubsystemHealth? For(EngineRoomSubsystem subsystem) =>
        SubsystemHealthList.FirstOrDefault(h => h.Subsystem == subsystem);
}

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Condition rating for a piece of equipment observed during an inspection.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DeficiencySeverity"/>: a Deficiency is by
/// definition something wrong; a <see cref="ConditionRating"/> is a
/// proactive ongoing-condition rating that any equipment carries (good →
/// failed). The two coexist on the same inspection.
/// </remarks>
public enum ConditionRating
{
    /// <summary>Equipment is in good working order; no concerns.</summary>
    Good,

    /// <summary>Equipment functions but shows wear; serviceable.</summary>
    Fair,

    /// <summary>Equipment is functional but degraded; replacement should be planned.</summary>
    Poor,

    /// <summary>Equipment has failed or is non-operational; immediate replacement needed.</summary>
    Failed,
}

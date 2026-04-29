using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A condition assessment of a specific piece of equipment recorded during an inspection.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Deficiency"/>: this is a proactive condition rating for any
/// equipment (Good / Fair / Poor / Failed); a Deficiency is by definition something wrong.
/// Both can coexist on the same inspection — e.g., a water heater rated <c>Poor</c>
/// (proactive degradation observation) plus a Deficiency for an unrelated cracked tile.
/// </remarks>
public sealed record EquipmentConditionAssessment
{
    /// <summary>Stable identifier for this assessment record.</summary>
    public required EquipmentConditionAssessmentId Id { get; init; }

    /// <summary>The inspection during which this assessment was recorded.</summary>
    public required InspectionId InspectionId { get; init; }

    /// <summary>FK to the <c>Sunfish.Blocks.PropertyEquipment.Equipment</c> being rated.</summary>
    public required EquipmentId EquipmentId { get; init; }

    /// <summary>Condition observed (<see cref="ConditionRating.Good"/> through <see cref="ConditionRating.Failed"/>).</summary>
    public required ConditionRating Condition { get; init; }

    /// <summary>Optional projection of remaining useful life in years; informs replacement planning.</summary>
    public int? ExpectedRemainingLifeYears { get; init; }

    /// <summary>Free-text observations captured by the inspector.</summary>
    public string? Observations { get; init; }

    /// <summary>Free-text recommendations (service, replace, monitor).</summary>
    public string? Recommendations { get; init; }

    /// <summary>
    /// Optional photo blob references captured at assessment time. Placeholder for
    /// the eventual Bridge blob-ingest pipeline (cluster cross-cutting OQ3); matches
    /// Deficiency's deferred-photo pattern (Deficiency itself has no photo field
    /// yet — this assessment ships the placeholder ahead of that integration).
    /// </summary>
    public IReadOnlyList<string> PhotoBlobRefs { get; init; } = Array.Empty<string>();

    /// <summary>Wall-clock instant the assessment was recorded.</summary>
    public required Instant ObservedAtUtc { get; init; }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A deficiency observed during an inspection.
/// Deficiencies are passive records in this pass — work-order rollup automation and
/// integration with blocks-maintenance are deferred to G16 second pass.
/// </summary>
/// <param name="Id">Unique identifier for this deficiency.</param>
/// <param name="InspectionId">The inspection during which this deficiency was observed.</param>
/// <param name="ItemId">The checklist item that triggered this deficiency.</param>
/// <param name="Severity">How severe the deficiency is (see <see cref="DeficiencySeverity"/>).</param>
/// <param name="Description">Human-readable description of the deficiency.</param>
/// <param name="ObservedAtUtc">The instant the deficiency was recorded.</param>
/// <param name="Status">Current status of the deficiency (see <see cref="DeficiencyStatus"/>).</param>
public sealed record Deficiency(
    DeficiencyId Id,
    InspectionId InspectionId,
    InspectionChecklistItemId ItemId,
    DeficiencySeverity Severity,
    string Description,
    Instant ObservedAtUtc,
    DeficiencyStatus Status);

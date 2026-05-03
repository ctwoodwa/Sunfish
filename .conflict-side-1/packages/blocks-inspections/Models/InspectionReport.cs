using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A snapshot summary of an inspection's outcome, generated after the inspection is completed.
/// </summary>
/// <param name="Id">Unique identifier for this report.</param>
/// <param name="InspectionId">The inspection this report summarises.</param>
/// <param name="GeneratedAtUtc">The instant this report was generated.</param>
/// <param name="Summary">Human-readable summary text.</param>
/// <param name="TotalItems">Total number of checklist items in the inspection template.</param>
/// <param name="PassedItems">
/// Number of items with a response that passes the implicit pass/fail heuristic
/// (YesNo=yes, PassFail=pass, Rating1to5 ≥ 3, FreeText/Photo = non-empty).
/// </param>
/// <param name="DeficiencyCount">Number of <see cref="Deficiency"/> records linked to this inspection.</param>
public sealed record InspectionReport(
    InspectionReportId Id,
    InspectionId InspectionId,
    Instant GeneratedAtUtc,
    string Summary,
    int TotalItems,
    int PassedItems,
    int DeficiencyCount);

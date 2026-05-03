using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A reusable template that defines the checklist for a class of inspections
/// (e.g., "Move-in inspection", "Annual HVAC inspection").
/// </summary>
/// <param name="Id">Unique identifier for this template.</param>
/// <param name="Name">Human-readable template name.</param>
/// <param name="Description">Optional description explaining the purpose or scope of this template.</param>
/// <param name="Items">Ordered list of checklist items that constitute this template.</param>
/// <param name="CreatedAtUtc">The instant at which the template was created.</param>
public sealed record InspectionTemplate(
    InspectionTemplateId Id,
    string Name,
    string? Description,
    IReadOnlyList<InspectionChecklistItem> Items,
    Instant CreatedAtUtc);

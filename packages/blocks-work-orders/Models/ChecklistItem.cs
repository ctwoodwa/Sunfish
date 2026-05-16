namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Inspection / verification step embedded in a
/// <see cref="MaintenanceTaskTemplate"/>; surfaces in the generated
/// <see cref="MaintenanceTask"/> for the assignee to tick off.
/// </summary>
/// <param name="Ordinal">Display order within the checklist (1-based).</param>
/// <param name="Text">Human-readable step description.</param>
/// <param name="IsMandatory">When true, the task cannot be completed until this step is ticked.</param>
public sealed record ChecklistItem(
    int Ordinal,
    string Text,
    bool IsMandatory = false);

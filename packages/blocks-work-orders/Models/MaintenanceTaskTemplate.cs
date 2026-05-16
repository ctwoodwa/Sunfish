namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Template embedded in a <see cref="MaintenanceSchedule"/> describing
/// the shape of each generated <see cref="MaintenanceTask"/> + the
/// corresponding <see cref="WorkOrder"/> the task elevates to when
/// dispatched.
/// </summary>
/// <param name="Title">Task title.</param>
/// <param name="Description">Optional detailed description.</param>
/// <param name="Priority">Generated work-order priority.</param>
/// <param name="Severity">Optional severity classification.</param>
/// <param name="AssignedToPartyId">Optional default assignee.</param>
/// <param name="ContractorId">Optional default contractor.</param>
/// <param name="EstimatedHours">Optional estimated labor time.</param>
/// <param name="EstimatedAmount">Optional estimated cost.</param>
/// <param name="EstimatedCurrency">Optional currency for the estimate.</param>
/// <param name="DefaultLines">Draft cost lines copied into the generated work order.</param>
/// <param name="ChecklistItems">Verification steps surfaced on the generated task.</param>
public sealed record MaintenanceTaskTemplate(
    string Title,
    Priority Priority,
    string? Description = null,
    WorkOrderSeverity? Severity = null,
    Guid? AssignedToPartyId = null,
    Guid? ContractorId = null,
    decimal? EstimatedHours = null,
    decimal? EstimatedAmount = null,
    string? EstimatedCurrency = null,
    IReadOnlyList<WorkOrderLineDraft>? DefaultLines = null,
    IReadOnlyList<ChecklistItem>? ChecklistItems = null);

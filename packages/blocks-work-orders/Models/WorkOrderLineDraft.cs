namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Value object embedded in <see cref="MaintenanceTaskTemplate"/>;
/// becomes a real <see cref="WorkOrderLine"/> when the template
/// generates a work order.
/// </summary>
/// <param name="Kind">Cost-component classifier.</param>
/// <param name="Description">Line description.</param>
/// <param name="EstimatedQuantity">Optional quantity (e.g., hours).</param>
/// <param name="EstimatedUnitPrice">Optional unit price.</param>
/// <param name="UnitOfMeasure">Optional UoM (e.g., "hr", "ea").</param>
public sealed record WorkOrderLineDraft(
    WorkOrderLineKind Kind,
    string Description,
    decimal? EstimatedQuantity = null,
    decimal? EstimatedUnitPrice = null,
    string? UnitOfMeasure = null);

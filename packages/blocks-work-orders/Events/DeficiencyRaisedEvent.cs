namespace Sunfish.Blocks.WorkOrders.Events;

/// <summary>
/// Cross-cluster input payload from <c>blocks-property-*</c>'s
/// deficiency-inspection flow per
/// <c>cross-cluster-event-bus-design.md</c> §3.2 (<c>Work.*</c>
/// section consumer side). When the property cluster records a new
/// deficiency, the work-orders cluster consumes the event via
/// <see cref="IDeficiencyRaisedHandler"/> and creates a corresponding
/// <see cref="Models.WorkOrder"/>.
/// </summary>
/// <param name="DeficiencyId">The deficiency record id (1:1 with the resulting WO via <c>WorkOrder.DeficiencyId</c>).</param>
/// <param name="PropertyId">FK to the property the deficiency belongs to (optional).</param>
/// <param name="UnitId">FK to the unit (optional).</param>
/// <param name="AssetId">FK to the asset (optional).</param>
/// <param name="Severity">String severity tag (mapped to <see cref="Models.WorkOrderSeverity"/> by the handler).</param>
/// <param name="Description">Human-readable deficiency description (becomes the WO title).</param>
/// <param name="DueBy">Optional SLA cutoff (required when severity maps to Safety / Habitability).</param>
public sealed record DeficiencyRaisedEvent(
    Guid DeficiencyId,
    Guid? PropertyId,
    Guid? UnitId,
    Guid? AssetId,
    string Severity,
    string Description,
    DateTimeOffset? DueBy = null);

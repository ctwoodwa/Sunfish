namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Lightweight tenant-facing repair-request sidecar per
/// <c>blocks-work-schema-design.md</c> §2.5. Submitted by tenants /
/// frontline staff; a triage step converts the ticket to a
/// <see cref="WorkOrder"/> (link recorded via
/// <see cref="ConvertedToWorkOrderId"/>).
/// </summary>
public sealed class RepairTicket
{
    public RepairTicketId Id { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Guid? RequestedByPartyId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public Guid? UnitId { get; private set; }
    public WorkOrderId? ConvertedToWorkOrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private RepairTicket(
        RepairTicketId id,
        string title,
        string? description,
        Guid? requestedByPartyId,
        Guid? propertyId,
        Guid? unitId,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id                 = id;
        Title              = title;
        Description        = description;
        RequestedByPartyId = requestedByPartyId;
        PropertyId         = propertyId;
        UnitId             = unitId;
        CreatedAt          = createdAt;
        UpdatedAt          = createdAt;
        CreatedBy          = createdBy;
        UpdatedBy          = createdBy;
    }

    public static RepairTicket Create(
        string title,
        Guid createdBy,
        string? description = null,
        Guid? requestedByPartyId = null,
        Guid? propertyId = null,
        Guid? unitId = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        return new RepairTicket(
            id:                 RepairTicketId.NewId(),
            title:              title,
            description:        description,
            requestedByPartyId: requestedByPartyId,
            propertyId:         propertyId,
            unitId:             unitId,
            createdAt:          createdAt ?? DateTimeOffset.UtcNow,
            createdBy:          createdBy);
    }

    /// <summary>
    /// Mark this ticket as converted to a <see cref="WorkOrder"/>.
    /// One-shot — subsequent calls throw to surface accidental
    /// double-conversion.
    /// </summary>
    public void ConvertTo(WorkOrderId workOrderId, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (ConvertedToWorkOrderId is not null)
            throw new InvalidOperationException(
                $"RepairTicket {Id.Value} is already converted to WorkOrder {ConvertedToWorkOrderId.Value.Value}.");
        ConvertedToWorkOrderId = workOrderId;
        UpdatedBy              = updatedBy;
        UpdatedAt              = updatedAt ?? DateTimeOffset.UtcNow;
    }
}

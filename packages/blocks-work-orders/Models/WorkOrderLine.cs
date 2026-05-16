namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Cost-component line on a <see cref="WorkOrder"/> per
/// <c>blocks-work-schema-design.md</c> §2.7. One line per cost
/// component; the total cost of the work order is the sum across all
/// non-deleted lines.
/// </summary>
// Inspired by Apache OFBiz WorkEffortLine (Apache 2.0) — clean-room expression.
public sealed class WorkOrderLine
{
    public WorkOrderLineId Id { get; private set; }
    public WorkOrderId WorkOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public WorkOrderLineKind Kind { get; private set; }
    public string Description { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public string? UnitOfMeasure { get; private set; }
    public string? Currency { get; private set; }
    public decimal? EstimatedAmount { get; private set; }
    public decimal? ActualAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private WorkOrderLine(
        WorkOrderLineId id,
        WorkOrderId workOrderId,
        int lineNumber,
        WorkOrderLineKind kind,
        string description,
        decimal? quantity,
        decimal? unitPrice,
        string? unitOfMeasure,
        string? currency,
        decimal? estimatedAmount,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id              = id;
        WorkOrderId     = workOrderId;
        LineNumber      = lineNumber;
        Kind            = kind;
        Description     = description;
        Quantity        = quantity;
        UnitPrice       = unitPrice;
        UnitOfMeasure   = unitOfMeasure;
        Currency        = currency;
        EstimatedAmount = estimatedAmount ?? DeriveEstimate(quantity, unitPrice);
        CreatedAt       = createdAt;
        UpdatedAt       = createdAt;
        CreatedBy       = createdBy;
        UpdatedBy       = createdBy;
    }

    /// <summary>
    /// Build a new <see cref="WorkOrderLine"/>. When
    /// <paramref name="estimatedAmount"/> is null and both
    /// <paramref name="quantity"/> and <paramref name="unitPrice"/>
    /// are non-null, the line's EstimatedAmount is computed as
    /// <paramref name="quantity"/> × <paramref name="unitPrice"/>.
    /// </summary>
    public static WorkOrderLine Create(
        WorkOrderId workOrderId,
        int lineNumber,
        WorkOrderLineKind kind,
        string description,
        Guid createdBy,
        decimal? quantity = null,
        decimal? unitPrice = null,
        string? unitOfMeasure = null,
        string? currency = null,
        decimal? estimatedAmount = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description must be non-empty.", nameof(description));
        return new WorkOrderLine(
            id:              WorkOrderLineId.NewId(),
            workOrderId:     workOrderId,
            lineNumber:      lineNumber,
            kind:            kind,
            description:     description,
            quantity:        quantity,
            unitPrice:       unitPrice,
            unitOfMeasure:   unitOfMeasure,
            currency:        currency,
            estimatedAmount: estimatedAmount,
            createdAt:       createdAt ?? DateTimeOffset.UtcNow,
            createdBy:       createdBy);
    }

    /// <summary>Record actual cost once incurred.</summary>
    public void SetActual(decimal actualAmount, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        ActualAmount = actualAmount;
        UpdatedBy    = updatedBy;
        UpdatedAt    = updatedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Soft-delete this line (preserved for audit).</summary>
    public void SoftDelete(Guid deletedBy, DateTimeOffset? deletedAt = null)
    {
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
        UpdatedBy = deletedBy;
        UpdatedAt = DeletedAt.Value;
    }

    private static decimal? DeriveEstimate(decimal? quantity, decimal? unitPrice)
        => quantity is { } q && unitPrice is { } p ? q * p : null;
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Work-order entity per <c>blocks-work-schema-design.md</c> §2.4. The
/// day-to-day execution unit for repairs, preventive maintenance, unit
/// turnovers, and inspection follow-ups across the
/// <c>blocks-property-*</c> portfolio.
/// </summary>
// Inspired by Apache OFBiz WorkEffort module (Apache 2.0) — clean-room expression.
public sealed class WorkOrder
{
    public WorkOrderId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Number { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public WorkOrderKind Kind { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public Priority Priority { get; private set; }
    public WorkOrderSeverity? Severity { get; private set; }

    public Guid? ProjectId { get; private set; }
    public Guid? PropertyId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? AssetId { get; private set; }
    public Guid? DeficiencyId { get; private set; }

    public Guid? RequestedByPartyId { get; private set; }
    public Guid? AssignedToPartyId { get; private set; }
    public Guid? ContractorId { get; private set; }

    public DateTimeOffset? ReportedAt { get; private set; }
    public DateTimeOffset? ScheduledStart { get; private set; }
    public DateTimeOffset? ScheduledEnd { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? DueBy { get; private set; }

    public decimal? EstimatedAmount { get; private set; }
    public string? EstimatedCurrency { get; private set; }
    public decimal? ActualAmount { get; private set; }

    public Guid? MaintenanceScheduleId { get; private set; }

    public bool TenantBillable { get; private set; }
    public Guid? RebillPartyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public long Version { get; private set; }

    private WorkOrder(
        WorkOrderId id,
        TenantId tenantId,
        string number,
        string title,
        WorkOrderKind kind,
        Priority priority,
        WorkOrderSeverity? severity,
        DateTimeOffset? dueBy,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id        = id;
        TenantId  = tenantId;
        Number    = number;
        Title     = title;
        Kind      = kind;
        Status    = WorkOrderStatus.New;
        Priority  = priority;
        Severity  = severity;
        DueBy     = dueBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
        Version   = 0;
    }

    /// <summary>
    /// Build a new <see cref="WorkOrder"/> in
    /// <see cref="WorkOrderStatus.New"/>. <paramref name="severity"/>
    /// values of <see cref="WorkOrderSeverity.Safety"/> or
    /// <see cref="WorkOrderSeverity.Habitability"/> require a non-null
    /// <paramref name="dueBy"/> (regulatory + SLA invariant).
    /// </summary>
    public static WorkOrder Create(
        TenantId tenantId,
        string title,
        WorkOrderKind kind,
        Priority priority,
        Guid createdBy,
        WorkOrderSeverity? severity = null,
        DateTimeOffset? dueBy = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        if ((severity == WorkOrderSeverity.Safety || severity == WorkOrderSeverity.Habitability) && dueBy is null)
            throw new ArgumentException(
                "Safety / Habitability severity requires a non-null DueBy at creation time.",
                nameof(dueBy));

        var now = createdAt ?? DateTimeOffset.UtcNow;
        var id = WorkOrderId.NewId();
        return new WorkOrder(
            id:        id,
            tenantId:  tenantId,
            number:    DeriveNumber(id, now),
            title:     title,
            kind:      kind,
            priority:  priority,
            severity:  severity,
            dueBy:     dueBy,
            createdAt: now,
            createdBy: createdBy);
    }

    /// <summary>
    /// Transition the work order to <paramref name="to"/>. Throws
    /// <see cref="InvalidStatusTransitionException"/> when the
    /// <see cref="WorkOrderStatusMachine"/> rejects the transition.
    /// </summary>
    public void Transition(WorkOrderStatus to, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (!WorkOrderStatusMachine.CanTransition(Status, to))
            throw new InvalidStatusTransitionException(Status, to);
        Status    = to;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
        // Capture the lifecycle timestamps the state diagram cares
        // about so downstream reports + audit can read them off the
        // entity directly.
        switch (to)
        {
            case WorkOrderStatus.InProgress when StartedAt is null:
                StartedAt = UpdatedAt;
                break;
            case WorkOrderStatus.Completed when CompletedAt is null:
                CompletedAt = UpdatedAt;
                break;
        }
    }

    /// <summary>
    /// Attach the cross-cluster anchor ids (property / unit / asset /
    /// deficiency / project / requestedByPartyId) at creation time
    /// (or shortly after). Anchors are nullable + idempotent —
    /// repeated calls overwrite. PR 4's service uses this to set
    /// <see cref="DeficiencyId"/> for the DeficiencyRaised handler's
    /// idempotency lookup.
    /// </summary>
    public void AttachAnchors(
        Guid? propertyId = null,
        Guid? unitId = null,
        Guid? assetId = null,
        Guid? deficiencyId = null,
        Guid? projectId = null,
        Guid? requestedByPartyId = null,
        DateTimeOffset? updatedAt = null)
    {
        PropertyId         = propertyId         ?? PropertyId;
        UnitId             = unitId             ?? UnitId;
        AssetId            = assetId            ?? AssetId;
        DeficiencyId       = deficiencyId       ?? DeficiencyId;
        ProjectId          = projectId          ?? ProjectId;
        RequestedByPartyId = requestedByPartyId ?? RequestedByPartyId;
        UpdatedAt          = updatedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Assign the work order to a party or contractor (or both).</summary>
    public void Assign(Guid? assignedToPartyId, Guid? contractorId, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        AssignedToPartyId = assignedToPartyId;
        ContractorId      = contractorId;
        UpdatedBy         = updatedBy;
        UpdatedAt         = updatedAt ?? DateTimeOffset.UtcNow;
        Version          += 1;
    }

    /// <summary>Update or set the estimated cost.</summary>
    public void UpdateEstimate(decimal amount, string currency, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must be non-empty.", nameof(currency));
        EstimatedAmount   = amount;
        EstimatedCurrency = currency;
        UpdatedBy         = updatedBy;
        UpdatedAt         = updatedAt ?? DateTimeOffset.UtcNow;
        Version          += 1;
    }

    /// <summary>
    /// Set or change <see cref="Severity"/>. When elevating to
    /// <see cref="WorkOrderSeverity.Safety"/> or
    /// <see cref="WorkOrderSeverity.Habitability"/>, the supplied
    /// <paramref name="dueBy"/> must be non-null.
    /// </summary>
    public void SetSeverity(WorkOrderSeverity severity, DateTimeOffset? dueBy, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if ((severity == WorkOrderSeverity.Safety || severity == WorkOrderSeverity.Habitability) && dueBy is null)
            throw new ArgumentException(
                "Safety / Habitability severity requires a non-null DueBy.",
                nameof(dueBy));
        Severity  = severity;
        DueBy     = dueBy ?? DueBy;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Soft-delete (preserved for audit + reporting).</summary>
    public void SoftDelete(Guid deletedBy, DateTimeOffset? deletedAt = null)
    {
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
        UpdatedBy = deletedBy;
        UpdatedAt = DeletedAt.Value;
        Version  += 1;
    }

    private static string DeriveNumber(WorkOrderId id, DateTimeOffset mintInstant)
    {
        // WO-{yyyyMMdd}-{first 7 hex chars of UUIDv7 id} per Stage 02
        // §2.4 — collision-free under CRDT because the id is itself
        // time-prefixed.
        var dateToken = mintInstant.ToString("yyyyMMdd");
        var hexToken  = id.Value.ToString("N")[..7];
        return $"WO-{dateToken}-{hexToken}";
    }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Milestone within a <see cref="Project"/> per Stage 02 §2.3. May
/// trigger an invoice when <see cref="TriggersInvoice"/> = true +
/// the project's customer is set (validated at Create).
/// </summary>
public sealed class ProjectMilestone
{
    public MilestoneId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public MilestoneKind Kind { get; private set; }
    public DateOnly PlannedDate { get; private set; }
    public DateOnly? ActualDate { get; private set; }
    public MilestoneStatus Status { get; private set; }
    public decimal? Weight { get; private set; }
    public decimal? PaymentAmount { get; private set; }
    public string? PaymentCurrency { get; private set; }
    public bool TriggersInvoice { get; private set; }
    public MilestoneId? PredecessorMilestoneId { get; private set; }
    public Guid? CustomerPartyId { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    private ProjectMilestone(
        MilestoneId id,
        TenantId tenantId,
        ProjectId projectId,
        string code,
        string name,
        MilestoneKind kind,
        DateOnly plannedDate,
        Guid createdBy,
        Instant createdAt)
    {
        Id          = id;
        TenantId    = tenantId;
        ProjectId   = projectId;
        Code        = code;
        Name        = name;
        Kind        = kind;
        PlannedDate = plannedDate;
        Status      = MilestoneStatus.Pending;
        CreatedAt   = createdAt;
        UpdatedAt   = createdAt;
        CreatedBy   = createdBy;
        UpdatedBy   = createdBy;
    }

    /// <summary>
    /// Build a new <see cref="ProjectMilestone"/> in
    /// <see cref="MilestoneStatus.Pending"/>. Validates the Payment +
    /// TriggersInvoice + Weight invariants per Stage 02 §2.3.
    /// </summary>
    public static ProjectMilestone Create(
        TenantId tenantId,
        MilestoneId id,
        ProjectId projectId,
        string code,
        string name,
        MilestoneKind kind,
        DateOnly plannedDate,
        Guid createdBy,
        Instant createdAt,
        decimal? paymentAmount = null,
        string? paymentCurrency = null,
        bool triggersInvoice = false,
        MilestoneId? predecessorMilestoneId = null,
        Guid? customerPartyId = null,
        decimal? weight = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code must be non-empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        if (kind == MilestoneKind.Payment && (paymentAmount is null || string.IsNullOrWhiteSpace(paymentCurrency)))
            throw new ArgumentException(
                "Payment milestones require both PaymentAmount + PaymentCurrency.",
                nameof(paymentAmount));
        if (triggersInvoice)
        {
            if (customerPartyId is null)
                throw new ArgumentException(
                    "TriggersInvoice = true requires CustomerPartyId.",
                    nameof(customerPartyId));
            if (paymentAmount is null)
                throw new ArgumentException(
                    "TriggersInvoice = true requires PaymentAmount.",
                    nameof(paymentAmount));
        }
        if (weight is { } w && (w < 0m || w > 1m))
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be in [0, 1].");

        return new ProjectMilestone(id, tenantId, projectId, code, name, kind, plannedDate, createdBy, createdAt)
        {
            Description            = description,
            PaymentAmount          = paymentAmount,
            PaymentCurrency        = paymentCurrency,
            TriggersInvoice        = triggersInvoice,
            PredecessorMilestoneId = predecessorMilestoneId,
            CustomerPartyId        = customerPartyId,
            Weight                 = weight,
        };
    }

    /// <summary>Flag at-risk; non-terminal observation flag.</summary>
    public void MarkAtRisk(Guid updatedBy, Instant updatedAt)
    {
        Status    = MilestoneStatus.AtRisk;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
        Version  += 1;
    }

    /// <summary>Mark achieved with actual date.</summary>
    public void Achieve(DateOnly actualDate, Guid updatedBy, Instant updatedAt)
    {
        ActualDate = actualDate;
        Status     = MilestoneStatus.Achieved;
        UpdatedBy  = updatedBy;
        UpdatedAt  = updatedAt;
        Version   += 1;
    }

    /// <summary>Mark as missed (planned date passed without achievement).</summary>
    public void Miss(Guid updatedBy, Instant updatedAt)
    {
        Status    = MilestoneStatus.Missed;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
        Version  += 1;
    }

    /// <summary>Cancel.</summary>
    public void Cancel(Guid updatedBy, Instant updatedAt)
    {
        Status    = MilestoneStatus.Cancelled;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
        Version  += 1;
    }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Project entity per Stage 02 §2.1. The top-level work container
/// for a coordinated set of <see cref="ProjectMilestone"/>s +
/// <c>WorkOrder</c>s (PR 6 service surface).
/// </summary>
// Inspired by Apache OFBiz WorkEffort + AgreementItem modules (Apache 2.0) — clean-room expression.
public sealed class Project
{
    public ProjectId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ProjectKind Kind { get; private set; }
    public ProjectStatus Status { get; private set; }
    public Priority Priority { get; private set; }

    public Guid? PropertyId { get; private set; }
    public Guid? AssetId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? CustomerPartyId { get; private set; }

    public ProjectId? ParentProjectId { get; private set; }

    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }

    public Guid OwnerPartyId { get; private set; }
    public Guid? SponsorPartyId { get; private set; }

    public decimal? BudgetedAmount { get; private set; }
    public string? BudgetedCurrency { get; private set; }
    public decimal? ActualAmount { get; private set; }
    public string? ActualCurrency { get; private set; }
    public decimal? PercentComplete { get; private set; }

    public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();
    public Instant? ArchivedAt { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    /// <summary>Max length of <see cref="Name"/> + <see cref="Code"/> + per-tag entry — DoS guard.</summary>
    public const int MaxNameLength = 300;

    /// <summary>Max length of a single <see cref="Tags"/> entry.</summary>
    public const int MaxTagLength = 300;

    private Project(
        ProjectId id,
        TenantId tenantId,
        string code,
        string name,
        ProjectKind kind,
        Priority priority,
        Guid ownerPartyId,
        Guid createdBy,
        Instant createdAt)
    {
        Id            = id;
        TenantId      = tenantId;
        Code          = code;
        Name          = name;
        Kind          = kind;
        Status        = ProjectStatus.Draft;
        Priority      = priority;
        OwnerPartyId  = ownerPartyId;
        CreatedAt     = createdAt;
        UpdatedAt     = createdAt;
        CreatedBy     = createdBy;
        UpdatedBy     = createdBy;
        Version       = 0;
    }

    /// <summary>
    /// Build a new <see cref="Project"/> in
    /// <see cref="ProjectStatus.Draft"/>. <paramref name="code"/> is
    /// pre-generated via <c>IProjectCodeGenerator</c> in the
    /// callers' service layer.
    /// </summary>
    public static Project Create(
        TenantId tenantId,
        ProjectId id,
        string code,
        string name,
        ProjectKind kind,
        Priority priority,
        Guid ownerPartyId,
        Guid createdBy,
        Instant createdAt,
        string? description = null,
        Guid? propertyId = null,
        Guid? assetId = null,
        Guid? unitId = null,
        Guid? customerPartyId = null,
        ProjectId? parentProjectId = null,
        Guid? sponsorPartyId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null,
        IReadOnlyList<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code must be non-empty.", nameof(code));
        if (code.Length > MaxNameLength)
            throw new ArgumentException($"Code exceeds MaxNameLength={MaxNameLength}.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        if (name.Length > MaxNameLength)
            throw new ArgumentException($"Name exceeds MaxNameLength={MaxNameLength}.", nameof(name));
        if (ownerPartyId == Guid.Empty)
            throw new ArgumentException("OwnerPartyId is required (designated authority per CRDT Pattern A).", nameof(ownerPartyId));
        if (plannedStartDate is { } pStart && plannedEndDate is { } pEnd && pEnd < pStart)
            throw new ArgumentException(
                $"PlannedEndDate {pEnd:O} must be >= PlannedStartDate {pStart:O}.",
                nameof(plannedEndDate));

        var p = new Project(id, tenantId, code, name, kind, priority, ownerPartyId, createdBy, createdAt)
        {
            Description       = description,
            PropertyId        = propertyId,
            AssetId           = assetId,
            UnitId            = unitId,
            CustomerPartyId   = customerPartyId,
            ParentProjectId   = parentProjectId,
            SponsorPartyId    = sponsorPartyId,
            PlannedStartDate  = plannedStartDate,
            PlannedEndDate    = plannedEndDate,
            Tags              = tags?.ToList() ?? new List<string>(),
        };
        return p;
    }

    /// <summary>Transition the project to <paramref name="to"/>. Throws on invalid transition.</summary>
    public void TransitionStatus(ProjectStatus to, Guid updatedBy, Instant updatedAt)
    {
        if (DeletedAt is not null)
            throw new InvalidOperationException(
                $"Project {Id.Value} is soft-deleted; transitions are rejected.");
        if (!ProjectStatusMachine.CanTransition(Status, to))
            throw new InvalidProjectStatusTransitionException(Status, to);
        Status    = to;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
        Version  += 1;
    }

    public void UpdatePlannedDates(DateOnly? start, DateOnly? end, Guid updatedBy, Instant updatedAt)
    {
        if (start is { } s && end is { } e && e < s)
            throw new ArgumentException("PlannedEndDate must be >= PlannedStartDate.", nameof(end));
        PlannedStartDate = start;
        PlannedEndDate   = end;
        UpdatedBy        = updatedBy;
        UpdatedAt        = updatedAt;
        Version         += 1;
    }

    public void RecordActualStart(DateOnly date, Guid updatedBy, Instant updatedAt)
    {
        ActualStartDate = date;
        UpdatedBy       = updatedBy;
        UpdatedAt       = updatedAt;
        Version        += 1;
    }

    public void RecordActualEnd(DateOnly date, Guid updatedBy, Instant updatedAt)
    {
        ActualEndDate = date;
        UpdatedBy     = updatedBy;
        UpdatedAt     = updatedAt;
        Version      += 1;
    }

    public void UpdateRollups(decimal? budgetedAmount, decimal? actualAmount, decimal? percentComplete, string? currency)
    {
        BudgetedAmount   = budgetedAmount;
        ActualAmount     = actualAmount;
        PercentComplete  = percentComplete;
        BudgetedCurrency = currency ?? BudgetedCurrency;
        ActualCurrency   = currency ?? ActualCurrency;
        Version         += 1;
    }

    /// <summary>Archive (distinct from soft-delete — archived projects remain queryable).</summary>
    public void Archive(Guid updatedBy, Instant updatedAt)
    {
        ArchivedAt = updatedAt;
        UpdatedBy  = updatedBy;
        UpdatedAt  = updatedAt;
        Version   += 1;
    }

    /// <summary>Soft-delete; subsequent transitions throw.</summary>
    public void SoftDelete(Guid deletedBy, Instant deletedAt)
    {
        DeletedAt = deletedAt;
        UpdatedBy = deletedBy;
        UpdatedAt = deletedAt;
        Version  += 1;
    }

    /// <summary>
    /// Replace the full <see cref="Tags"/> collection. Intended for
    /// importer-style flows that maintain external-system metadata
    /// tags (<c>externalRef:erpnext:&lt;name&gt;</c> +
    /// <c>erpnextModified:&lt;version&gt;</c>). Set-semantics: callers
    /// pass the entire desired post-state. Each tag is bounded at
    /// <see cref="MaxTagLength"/> + rejected if it contains
    /// <c>\r</c>/<c>\n</c> (newline-injection guard — downstream log /
    /// projection consumers parse tags line-by-line).
    /// </summary>
    internal void OverwriteTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var list = tags.ToList();
        foreach (var t in list)
        {
            if (t is null) throw new ArgumentException("Tag entry cannot be null.", nameof(tags));
            if (t.Length > MaxTagLength)
                throw new ArgumentException(
                    $"Tag entry exceeds MaxTagLength={MaxTagLength}.", nameof(tags));
            if (t.Contains('\r') || t.Contains('\n'))
                throw new ArgumentException(
                    "Tag entry must not contain CR or LF characters.", nameof(tags));
        }
        Tags = list;
        Version += 1;
    }
}

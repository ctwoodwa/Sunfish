using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Recurring preventive-maintenance schedule per
/// <c>blocks-work-schema-design.md</c> §2.9. Drives generation of
/// <see cref="MaintenanceTask"/> instances + the corresponding
/// <see cref="WorkOrder"/>s.
/// </summary>
// MaintenanceSchedule.RecurrenceRule format: RFC 5545 §3.3.10 (IETF; open standard).
public sealed class MaintenanceSchedule
{
    public MaintenanceScheduleId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    public Guid? PropertyId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? AssetId { get; private set; }

    public string RecurrenceRule { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public string Timezone { get; private set; }

    public MaintenanceTaskTemplate TaskTemplate { get; private set; }
    public int GenerateLeadDays { get; private set; }
    public int LookaheadHorizonDays { get; private set; }

    public ScheduleStatus Status { get; private set; }
    public DateTimeOffset? LastGeneratedAt { get; private set; }
    public DateOnly? NextDueAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public long Version { get; private set; }

    private MaintenanceSchedule(
        MaintenanceScheduleId id,
        TenantId tenantId,
        string name,
        string recurrenceRule,
        DateOnly startsOn,
        string timezone,
        MaintenanceTaskTemplate taskTemplate,
        int generateLeadDays,
        int lookaheadHorizonDays,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id                   = id;
        TenantId             = tenantId;
        Name                 = name;
        RecurrenceRule       = recurrenceRule;
        StartsOn             = startsOn;
        Timezone             = timezone;
        TaskTemplate         = taskTemplate;
        GenerateLeadDays     = generateLeadDays;
        LookaheadHorizonDays = lookaheadHorizonDays;
        Status               = ScheduleStatus.Active;
        CreatedAt            = createdAt;
        UpdatedAt            = createdAt;
        CreatedBy            = createdBy;
        UpdatedBy            = createdBy;
        Version              = 0;
    }

    /// <summary>
    /// Build a new <see cref="MaintenanceSchedule"/> in
    /// <see cref="ScheduleStatus.Active"/>.
    /// </summary>
    public static MaintenanceSchedule Create(
        TenantId tenantId,
        string name,
        string recurrenceRule,
        DateOnly startsOn,
        string timezone,
        MaintenanceTaskTemplate taskTemplate,
        Guid createdBy,
        int generateLeadDays = 7,
        int lookaheadHorizonDays = 90,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must be non-empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(recurrenceRule))
            throw new ArgumentException("RecurrenceRule must be non-empty.", nameof(recurrenceRule));
        if (string.IsNullOrWhiteSpace(timezone))
            throw new ArgumentException("Timezone must be non-empty (IANA tz id).", nameof(timezone));
        ArgumentNullException.ThrowIfNull(taskTemplate);
        if (generateLeadDays < 0)
            throw new ArgumentOutOfRangeException(nameof(generateLeadDays), "Must be ≥ 0.");
        if (lookaheadHorizonDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(lookaheadHorizonDays), "Must be > 0.");

        return new MaintenanceSchedule(
            id:                   MaintenanceScheduleId.NewId(),
            tenantId:             tenantId,
            name:                 name,
            recurrenceRule:       recurrenceRule,
            startsOn:             startsOn,
            timezone:             timezone,
            taskTemplate:         taskTemplate,
            generateLeadDays:     generateLeadDays,
            lookaheadHorizonDays: lookaheadHorizonDays,
            createdAt:            createdAt ?? DateTimeOffset.UtcNow,
            createdBy:            createdBy);
    }

    /// <summary>
    /// Optional scope hints (property / unit / asset) — at least one
    /// is recommended for downstream UI grouping. Set together for
    /// atomicity.
    /// </summary>
    public void SetScope(Guid? propertyId, Guid? unitId, Guid? assetId, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        PropertyId = propertyId;
        UnitId     = unitId;
        AssetId    = assetId;
        UpdatedBy  = updatedBy;
        UpdatedAt  = updatedAt ?? DateTimeOffset.UtcNow;
        Version   += 1;
    }

    /// <summary>Pause generation; status flips Active → Paused.</summary>
    public void Pause(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (Status == ScheduleStatus.Archived)
            throw new InvalidOperationException("Cannot pause an Archived schedule.");
        Status    = ScheduleStatus.Paused;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Resume generation; status flips Paused → Active.</summary>
    public void Resume(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (Status != ScheduleStatus.Paused)
            throw new InvalidOperationException($"Cannot resume from status {Status}.");
        Status    = ScheduleStatus.Active;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Archive — terminal state; cannot be resumed.</summary>
    public void Archive(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        Status    = ScheduleStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Record that the generator ran + the new NextDueAt cursor.</summary>
    public void StampGenerated(DateTimeOffset generatedAt, DateOnly? nextDueAt, Guid updatedBy)
    {
        LastGeneratedAt = generatedAt;
        NextDueAt       = nextDueAt;
        UpdatedBy       = updatedBy;
        UpdatedAt       = generatedAt;
        Version        += 1;
    }

    /// <summary>End the schedule on a specific date (e.g., asset retired).</summary>
    public void SetEndsOn(DateOnly endsOn, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (endsOn < StartsOn)
            throw new ArgumentException("EndsOn must be on or after StartsOn.", nameof(endsOn));
        EndsOn    = endsOn;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }
}

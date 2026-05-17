using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Time-tracking entry per Stage 02 §2.20. Targets exactly one of
/// <see cref="ProjectId"/> / <see cref="WorkOrderId"/> /
/// <see cref="MaintenanceTaskId"/>. Append-only after
/// <see cref="TimeEntryStatus.Approved"/> per
/// <c>crdt-friendly-schema-conventions.md</c> §6 — corrections via
/// a new entry + a reversing entry.
/// </summary>
public sealed class TimeEntry
{
    public TimeEntryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid WorkerPartyId { get; private set; }

    public ProjectId? ProjectId { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public Guid? MaintenanceTaskId { get; private set; }

    public ActivityKind ActivityKind { get; private set; }
    public Instant StartedAt { get; private set; }
    public Instant? EndedAt { get; private set; }
    public int DurationMinutes { get; private set; }

    public bool Billable { get; private set; }
    public decimal? HourlyRate { get; private set; }
    public string? HourlyRateCurrency { get; private set; }
    public decimal? Amount { get; private set; }
    public Guid? GlAccountId { get; private set; }
    public string? Description { get; private set; }

    public TimeEntryStatus Status { get; private set; }
    public Instant? SubmittedAt { get; private set; }
    public Guid? ApprovedByPartyId { get; private set; }
    public Instant? ApprovedAt { get; private set; }
    public Guid? RejectedByPartyId { get; private set; }
    public Instant? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public bool InvoicedFlag { get; private set; }

    /// <summary>Max length of <see cref="Description"/> + <see cref="RejectionReason"/> — DoS guard at the model boundary.</summary>
    public const int MaxFreeTextLength = 4000;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    private TimeEntry(
        TimeEntryId id,
        TenantId tenantId,
        Guid workerPartyId,
        ActivityKind activityKind,
        Instant startedAt,
        bool billable,
        Guid createdBy,
        Instant createdAt)
    {
        Id            = id;
        TenantId      = tenantId;
        WorkerPartyId = workerPartyId;
        ActivityKind  = activityKind;
        StartedAt     = startedAt;
        Billable      = billable;
        Status        = TimeEntryStatus.Open;
        CreatedAt     = createdAt;
        UpdatedAt     = createdAt;
        CreatedBy     = createdBy;
        UpdatedBy     = createdBy;
    }

    /// <summary>
    /// Build a new running <see cref="TimeEntry"/>. Exactly one of
    /// <paramref name="projectId"/> / <paramref name="workOrderId"/>
    /// / <paramref name="maintenanceTaskId"/> must be non-null.
    /// </summary>
    public static TimeEntry Open(
        TenantId tenantId,
        TimeEntryId id,
        Guid workerPartyId,
        ActivityKind activityKind,
        Instant startedAt,
        Guid createdBy,
        Instant createdAt,
        ProjectId? projectId = null,
        Guid? workOrderId = null,
        Guid? maintenanceTaskId = null,
        bool billable = true,
        Guid? glAccountId = null,
        string? description = null)
    {
        if (workerPartyId == Guid.Empty)
            throw new ArgumentException("WorkerPartyId is required.", nameof(workerPartyId));

        var targetCount = (projectId is not null ? 1 : 0)
                          + (workOrderId is not null ? 1 : 0)
                          + (maintenanceTaskId is not null ? 1 : 0);
        if (targetCount != 1)
            throw new ArgumentException(
                "TimeEntry requires exactly one of (ProjectId, WorkOrderId, MaintenanceTaskId) — "
                + $"received {targetCount}.");

        if (description is { Length: > MaxFreeTextLength })
            throw new ArgumentException(
                $"Description exceeds MaxFreeTextLength={MaxFreeTextLength}.", nameof(description));

        return new TimeEntry(id, tenantId, workerPartyId, activityKind, startedAt, billable, createdBy, createdAt)
        {
            ProjectId         = projectId,
            WorkOrderId       = workOrderId,
            MaintenanceTaskId = maintenanceTaskId,
            GlAccountId       = glAccountId,
            Description       = description,
        };
    }

    /// <summary>
    /// Stop the timer. Computes <see cref="DurationMinutes"/> +
    /// <see cref="Amount"/> when an hourly rate is provided.
    /// </summary>
    public void Stop(Instant endedAt, decimal? hourlyRate, string? rateCurrency, Guid updatedBy)
    {
        if (Status != TimeEntryStatus.Open)
            throw new InvalidOperationException(
                $"Cannot Stop a TimeEntry in status {Status}.");
        if (endedAt.Value < StartedAt.Value)
            throw new ArgumentException(
                "EndedAt must be >= StartedAt.", nameof(endedAt));

        EndedAt         = endedAt;
        DurationMinutes = (int)Math.Floor((endedAt.Value - StartedAt.Value).TotalMinutes);
        if (hourlyRate is { } rate)
        {
            if (string.IsNullOrWhiteSpace(rateCurrency))
                throw new ArgumentException(
                    "RateCurrency required when HourlyRate is provided.", nameof(rateCurrency));
            var normalized = rateCurrency.ToUpperInvariant();
            if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
                throw new ArgumentException(
                    $"RateCurrency '{rateCurrency}' is not a 3-letter ISO-4217 code.", nameof(rateCurrency));
            HourlyRate         = rate;
            HourlyRateCurrency = normalized;
            // AwayFromZero rounding — industry-standard money "round half up" for positive amounts.
            Amount             = Math.Round(rate * DurationMinutes / 60m, 2, MidpointRounding.AwayFromZero);
        }
        UpdatedBy = updatedBy;
        UpdatedAt = endedAt;
        Version  += 1;
    }

    /// <summary>Transition Open → Submitted.</summary>
    public void Submit(Instant submittedAt, Guid updatedBy)
    {
        if (Status != TimeEntryStatus.Open)
            throw new InvalidOperationException(
                $"Cannot Submit a TimeEntry in status {Status}.");
        if (EndedAt is null)
            throw new InvalidOperationException(
                "Cannot Submit a TimeEntry that is still running — call Stop first.");

        Status      = TimeEntryStatus.Submitted;
        SubmittedAt = submittedAt;
        UpdatedBy   = updatedBy;
        UpdatedAt   = submittedAt;
        Version    += 1;
    }

    /// <summary>Update description (allowed in Open + Submitted only).</summary>
    public void UpdateDescription(string description, Guid updatedBy, Instant updatedAt)
    {
        if (Status != TimeEntryStatus.Open && Status != TimeEntryStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot UpdateDescription on a TimeEntry in status {Status} — corrections require a new entry.");
        if (description is { Length: > MaxFreeTextLength })
            throw new ArgumentException(
                $"Description exceeds MaxFreeTextLength={MaxFreeTextLength}.", nameof(description));
        Description = description;
        UpdatedBy   = updatedBy;
        UpdatedAt   = updatedAt;
        Version    += 1;
    }

    /// <summary>Approve. Callable only by the approval service in this assembly.</summary>
    internal void Approve(Guid approverPartyId, Instant approvedAt)
    {
        if (Status != TimeEntryStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot Approve a TimeEntry in status {Status}.");
        Status            = TimeEntryStatus.Approved;
        ApprovedByPartyId = approverPartyId;
        ApprovedAt        = approvedAt;
        UpdatedBy         = approverPartyId;
        UpdatedAt         = approvedAt;
        Version          += 1;
    }

    /// <summary>
    /// Reject with reason. Callable only by the approval service in this
    /// assembly. Stores <paramref name="rejecterPartyId"/> on
    /// <see cref="RejectedByPartyId"/> (NOT <see cref="ApprovedByPartyId"/>)
    /// — read-side projections + UI must distinguish approve vs reject
    /// authority.
    /// </summary>
    internal void Reject(string reason, Guid rejecterPartyId, Instant rejectedAt)
    {
        if (Status != TimeEntryStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot Reject a TimeEntry in status {Status}.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));
        if (reason.Length > MaxFreeTextLength)
            throw new ArgumentException(
                $"Rejection reason exceeds MaxFreeTextLength={MaxFreeTextLength}.", nameof(reason));
        Status            = TimeEntryStatus.Rejected;
        RejectionReason   = reason;
        RejectedByPartyId = rejecterPartyId;
        RejectedAt        = rejectedAt;
        UpdatedBy         = rejecterPartyId;
        UpdatedAt         = rejectedAt;
        Version          += 1;
    }

    /// <summary>
    /// One-way mark-as-invoiced — called by the financial cluster
    /// reactor when this entry rolls into an invoice. The only
    /// permitted post-approval mutation. Idempotent: a re-fire from the
    /// reactor (e.g., at-least-once delivery) is a no-op when the entry
    /// is already <see cref="TimeEntryStatus.Invoiced"/>.
    /// </summary>
    public void MarkInvoiced(Guid updatedBy, Instant updatedAt)
    {
        if (Status == TimeEntryStatus.Invoiced && InvoicedFlag)
            return;
        if (Status != TimeEntryStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot MarkInvoiced a TimeEntry in status {Status}.");
        Status       = TimeEntryStatus.Invoiced;
        InvoicedFlag = true;
        UpdatedBy    = updatedBy;
        UpdatedAt    = updatedAt;
        Version     += 1;
    }
}

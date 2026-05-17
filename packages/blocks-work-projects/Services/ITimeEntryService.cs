using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Write surface for <see cref="TimeEntry"/> lifecycle (Open → Stop
/// → Submit). Approval / Reject lives on
/// <see cref="ITimeApprovalService"/> so the write + approve
/// authorities can be split at the host's composition root.
/// </summary>
public interface ITimeEntryService
{
    /// <summary>Open a new running <see cref="TimeEntry"/>.</summary>
    Task<TimeEntry> OpenAsync(
        TenantId tenantId,
        Guid workerPartyId,
        ActivityKind activityKind,
        Instant startedAt,
        Guid createdBy,
        ProjectId? projectId = null,
        Guid? workOrderId = null,
        Guid? maintenanceTaskId = null,
        bool billable = true,
        Guid? glAccountId = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stop a running entry; captures hourly rate at stop-time.</summary>
    Task<TimeEntry> StopAsync(
        TimeEntryId id,
        Instant endedAt,
        decimal? hourlyRate,
        string? rateCurrency,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Open → Submitted; emits
    /// <c>Work.TimeEntrySubmitted</c>. Period-gating is the caller's
    /// responsibility (PR 6 service-layer compose with
    /// <c>IPeriodResolver</c> when chart context is available).
    /// </summary>
    Task<TimeEntry> SubmitAsync(
        TimeEntryId id,
        Instant submittedAt,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Update description on Open / Submitted entries only.</summary>
    Task UpdateDescriptionAsync(
        TimeEntryId id,
        string description,
        Guid updatedBy,
        Instant updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Fetch by id within the tenant. Returns null on tenant mismatch (H5).</summary>
    Task<TimeEntry?> GetByIdAsync(
        TenantId tenantId,
        TimeEntryId id,
        CancellationToken cancellationToken = default);
}

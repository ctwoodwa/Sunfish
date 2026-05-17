using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.TimeEntrySubmitted</c> per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c>
/// §3.2 catalog. Emitted by
/// <c>ITimeEntryService.SubmitAsync</c> on Open → Submitted.
/// </summary>
/// <remarks>
/// Canonical event-type: <c>Work.TimeEntrySubmitted</c>.
/// Idempotency-key format: <c>time-entry-submitted:{timeEntryId}</c>
/// (one-shot per submission — re-submission via correction-entry pattern).
/// </remarks>
public sealed record TimeEntrySubmittedEvent(
    TimeEntryId TimeEntryId,
    TenantId TenantId,
    Guid WorkerPartyId,
    ProjectId? ProjectId,
    Guid? WorkOrderId,
    Guid? MaintenanceTaskId,
    int DurationMinutes,
    bool Billable,
    decimal? Amount,
    string? Currency,
    Instant SubmittedAt);

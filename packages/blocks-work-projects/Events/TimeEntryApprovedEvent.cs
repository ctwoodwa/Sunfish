using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// Payload for <c>Work.TimeEntryApproved</c> per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c>
/// §3.2 catalog. Emitted by
/// <c>ITimeApprovalService.ApproveAsync</c> on Submitted → Approved.
/// </summary>
/// <remarks>
/// Canonical event-type: <c>Work.TimeEntryApproved</c>.
/// Idempotency-key format: <c>time-entry-approved:{timeEntryId}</c>
/// (one-shot per approval — re-approval impossible since the entity
/// is posted-then-immutable after Approve).
/// </remarks>
public sealed record TimeEntryApprovedEvent(
    TimeEntryId TimeEntryId,
    TenantId TenantId,
    Guid WorkerPartyId,
    Guid ApprovedByPartyId,
    ProjectId? ProjectId,
    Guid? WorkOrderId,
    Guid? MaintenanceTaskId,
    int DurationMinutes,
    bool Billable,
    decimal? Amount,
    string? Currency,
    Instant ApprovedAt);

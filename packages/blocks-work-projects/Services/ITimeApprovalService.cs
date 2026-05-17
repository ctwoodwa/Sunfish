using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Approval / Reject surface for <see cref="TimeEntry"/>. Split from
/// <see cref="ITimeEntryService"/> so callers can gate approval on a
/// distinct role (e.g., supervisor / project manager) without
/// granting the broader write permission.
/// </summary>
public interface ITimeApprovalService
{
    /// <summary>
    /// Transition Submitted → Approved. Emits
    /// <c>Work.TimeEntryApproved</c>. Caller enforces approver-role
    /// authorization — this service intentionally does not consult
    /// <c>IUserContext</c>.
    /// </summary>
    Task<TimeEntry> ApproveAsync(
        TimeEntryId id,
        Guid approverPartyId,
        Instant approvedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Submitted → Rejected with reason.
    /// </summary>
    Task<TimeEntry> RejectAsync(
        TimeEntryId id,
        Guid approverPartyId,
        Instant rejectedAt,
        string reason,
        CancellationToken cancellationToken = default);
}

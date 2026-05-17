using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Approval / Reject surface for <see cref="TimeEntry"/>. Split from
/// <see cref="ITimeEntryService"/> so callers can gate approval on a
/// distinct role (e.g., supervisor / project manager) without
/// granting the broader write permission. Every method takes
/// <see cref="TenantId"/> + enforces the H5 cross-tenant gate
/// (mismatch → <see cref="InvalidOperationException"/>).
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
        TenantId tenantId,
        TimeEntryId id,
        Guid approverPartyId,
        Instant approvedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Submitted → Rejected with reason. Stores the
    /// rejecter's party-id on <see cref="TimeEntry.RejectedByPartyId"/>
    /// (NOT <see cref="TimeEntry.ApprovedByPartyId"/>) so read-side
    /// projections can distinguish approve vs reject authority.
    /// </summary>
    Task<TimeEntry> RejectAsync(
        TenantId tenantId,
        TimeEntryId id,
        Guid rejecterPartyId,
        Instant rejectedAt,
        string reason,
        CancellationToken cancellationToken = default);
}

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="MaintenanceRequest"/>.
/// </summary>
/// <remarks>
/// Allowed transitions:
/// <code>
/// Submitted → UnderReview
/// UnderReview → Approved | Rejected
/// Approved → InProgress
/// InProgress → Completed
/// * → Cancelled  (from any non-terminal state)
/// </code>
/// Terminal states: <see cref="Completed"/>, <see cref="Rejected"/>, <see cref="Cancelled"/>.
/// </remarks>
public enum MaintenanceRequestStatus
{
    /// <summary>Request has been submitted and is awaiting review.</summary>
    Submitted,

    /// <summary>Request is being reviewed by property management.</summary>
    UnderReview,

    /// <summary>Request has been approved and work is being arranged.</summary>
    Approved,

    /// <summary>Request has been rejected.</summary>
    Rejected,

    /// <summary>Work is actively in progress.</summary>
    InProgress,

    /// <summary>Work has been completed.</summary>
    Completed,

    /// <summary>Request has been cancelled.</summary>
    Cancelled,
}

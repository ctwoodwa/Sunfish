using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Write surface for <see cref="Project"/> + <see cref="ProjectMilestone"/>
/// orchestration. Emits cross-cluster events per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §3.2.
/// </summary>
/// <remarks>
/// AUTHORIZATION CONTRACT: Authorization is the caller's responsibility.
/// Status transitions enforce Pattern A (designated authority — the
/// <c>actingPartyId</c> must equal <see cref="Project.OwnerPartyId"/>);
/// callers MUST verify the caller's session principal matches before
/// invoking <see cref="TransitionStatusAsync"/>. Cross-entity completion
/// checks (e.g. all WorkOrders closed before allowing Completed) are
/// deferred to a follow-on hand-off — this PR ships state-machine +
/// designated-authority gates only.
/// </remarks>
public interface IProjectService
{
    Task<Project> CreateAsync(
        TenantId tenantId,
        string name,
        ProjectKind kind,
        Priority priority,
        Guid ownerPartyId,
        Guid createdBy,
        string? description = null,
        Guid? propertyId = null,
        Guid? customerPartyId = null,
        ProjectId? parentProjectId = null,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null,
        CancellationToken cancellationToken = default);

    Task<Project> TransitionStatusAsync(
        TenantId tenantId,
        ProjectId id,
        ProjectStatus to,
        Guid actingPartyId,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    Task<ProjectMilestone> AddMilestoneAsync(
        TenantId tenantId,
        ProjectId projectId,
        string code,
        string name,
        MilestoneKind kind,
        DateOnly plannedDate,
        Guid createdBy,
        decimal? weight = null,
        decimal? paymentAmount = null,
        string? paymentCurrency = null,
        bool triggersInvoice = false,
        Guid? customerPartyId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectMilestone> AchieveMilestoneAsync(
        TenantId tenantId,
        MilestoneId id,
        DateOnly actualDate,
        Guid updatedBy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when <see cref="IProjectService.TransitionStatusAsync"/> is
/// invoked by a party other than <see cref="Project.OwnerPartyId"/>
/// (Pattern A — designated authority).
/// </summary>
public sealed class NotProjectOwnerException : InvalidOperationException
{
    public ProjectId ProjectId { get; }
    public Guid AttemptedByPartyId { get; }

    public NotProjectOwnerException(ProjectId projectId, Guid attemptedByPartyId)
        : base($"Party {attemptedByPartyId} is not the OwnerPartyId of Project {projectId.Value}; status transitions require the designated authority.")
    {
        ProjectId          = projectId;
        AttemptedByPartyId = attemptedByPartyId;
    }
}

using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Cross-cluster read accessor for <see cref="Project"/>. Other
/// clusters (e.g. <c>blocks-work-orders</c> verifying a project
/// exists before linking a WO) consume this rather than reaching
/// into the repository directly.
/// </summary>
public interface IProjectReadModel
{
    Task<Project?> GetByIdAsync(
        TenantId tenantId,
        ProjectId id,
        CancellationToken cancellationToken = default);

    Task<ProjectSummary?> GetSummaryAsync(
        TenantId tenantId,
        ProjectId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMilestone>> GetMilestonesAsync(
        TenantId tenantId,
        ProjectId id,
        CancellationToken cancellationToken = default);
}

/// <summary>Compact projection — useful for UI list views + cross-cluster summaries.</summary>
public sealed record ProjectSummary(
    ProjectId Id,
    string Code,
    string Name,
    ProjectStatus Status,
    ProjectKind Kind);

/// <summary>Default <see cref="IProjectReadModel"/>.</summary>
public sealed class InMemoryProjectReadModel : IProjectReadModel
{
    private readonly InMemoryProjectRepository _projects;
    private readonly InMemoryProjectMilestoneRepository _milestones;

    public InMemoryProjectReadModel(InMemoryProjectRepository projects, InMemoryProjectMilestoneRepository milestones)
    {
        _projects   = projects   ?? throw new ArgumentNullException(nameof(projects));
        _milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
    }

    public Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_projects.GetById(tenantId, id));

    public Task<ProjectSummary?> GetSummaryAsync(TenantId tenantId, ProjectId id, CancellationToken cancellationToken = default)
    {
        var p = _projects.GetById(tenantId, id);
        return Task.FromResult(p is null ? null : new ProjectSummary(p.Id, p.Code, p.Name, p.Status, p.Kind));
    }

    public Task<IReadOnlyList<ProjectMilestone>> GetMilestonesAsync(TenantId tenantId, ProjectId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_milestones.GetByProject(tenantId, id));
}

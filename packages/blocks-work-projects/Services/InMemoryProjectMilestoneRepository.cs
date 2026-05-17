using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>In-memory store for <see cref="ProjectMilestone"/>. Tenant-scoped reads enforce H5.</summary>
public sealed class InMemoryProjectMilestoneRepository
{
    private readonly ConcurrentDictionary<MilestoneId, ProjectMilestone> _milestones = new();

    public void Upsert(ProjectMilestone m)
    {
        ArgumentNullException.ThrowIfNull(m);
        _milestones[m.Id] = m;
    }

    public ProjectMilestone? GetById(TenantId tenantId, MilestoneId id)
    {
        if (!_milestones.TryGetValue(id, out var m)) return null;
        return m.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal) ? m : null;
    }

    public IReadOnlyList<ProjectMilestone> GetByProject(TenantId tenantId, ProjectId projectId)
        => _milestones.Values
            .Where(m => m.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && m.ProjectId.Value == projectId.Value
                        && m.DeletedAt is null)
            .OrderBy(m => m.PlannedDate)
            .ToList();
}

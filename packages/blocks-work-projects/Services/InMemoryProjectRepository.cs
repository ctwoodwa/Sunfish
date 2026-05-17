using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// In-memory store for <see cref="Project"/>. Tenant-scoped reads
/// enforce H5. Lookup-by-Code + tenant-list support the ERPNext
/// importer's upsert-by-external-ref path.
/// </summary>
public sealed class InMemoryProjectRepository
{
    private readonly ConcurrentDictionary<ProjectId, Project> _projects = new();

    public void Upsert(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        _projects[project.Id] = project;
    }

    public Project? GetById(TenantId tenantId, ProjectId id)
    {
        if (!_projects.TryGetValue(id, out var p)) return null;
        return p.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal) ? p : null;
    }

    public Project? GetByCode(TenantId tenantId, string code)
        // Project codes are deterministic identifiers (PRJ-2026-L00001
        // pattern), not user free-text — Ordinal comparison prevents
        // case-folding collisions across generator changes.
        => _projects.Values.FirstOrDefault(p =>
            p.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
            && p.Code.Equals(code, StringComparison.Ordinal)
            && p.DeletedAt is null);

    public IReadOnlyList<Project> ListByTenant(TenantId tenantId)
        => _projects.Values
            .Where(p => p.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && p.DeletedAt is null)
            .ToList();
}

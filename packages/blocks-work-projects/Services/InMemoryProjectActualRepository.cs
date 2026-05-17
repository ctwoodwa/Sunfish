using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>In-memory <see cref="IProjectActualRepository"/>. Tenant-keyed reads enforce H5.</summary>
public sealed class InMemoryProjectActualRepository : IProjectActualRepository
{
    private readonly ConcurrentDictionary<ProjectActualId, ProjectActual> _rows = new();

    public Task<ProjectActual?> FindAsync(
        TenantId tenantId, ProjectId projectId, ActualSourceKind sourceKind, Guid? sourceRefId,
        Guid? glAccountId,
        CancellationToken cancellationToken = default)
    {
        var match = _rows.Values.FirstOrDefault(r =>
            r.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
            && r.ProjectId.Value == projectId.Value
            && r.SourceKind == sourceKind
            && Nullable.Equals(r.SourceRefId, sourceRefId)
            && Nullable.Equals(r.GlAccountId, glAccountId)
            && r.DeletedAt is null);
        return Task.FromResult(match);
    }

    public Task InsertAsync(ProjectActual actual, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (!_rows.TryAdd(actual.Id, actual))
            throw new InvalidOperationException(
                $"ProjectActual {actual.Id.Value} already exists.");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectActual>> GetByProjectAsync(
        TenantId tenantId, ProjectId projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectActual> rows = _rows.Values
            .Where(r => r.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && r.ProjectId.Value == projectId.Value
                        && r.DeletedAt is null)
            .OrderBy(r => r.PostedDate)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<ProjectActual>> GetByCategoryAsync(
        TenantId tenantId, ProjectId projectId, BudgetCategory category, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectActual> rows = _rows.Values
            .Where(r => r.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && r.ProjectId.Value == projectId.Value
                        && r.Category == category
                        && r.DeletedAt is null)
            .OrderBy(r => r.PostedDate)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyDictionary<BudgetCategory, decimal>> GetTotalsAsync(
        TenantId tenantId, ProjectId projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<BudgetCategory, decimal> totals = _rows.Values
            .Where(r => r.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && r.ProjectId.Value == projectId.Value
                        && r.DeletedAt is null)
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.PostedAmount));
        return Task.FromResult(totals);
    }
}

using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Read + insert surface for <see cref="ProjectActual"/>. Inserts are
/// projector-only; user code MUST NOT call <see cref="InsertAsync"/>
/// directly. Reads enforce H5 tenant isolation.
/// </summary>
public interface IProjectActualRepository
{
    /// <summary>
    /// Composite-key idempotency lookup. Returns the existing row if
    /// already projected for this (projectId, sourceKind, sourceRefId)
    /// triple, else null. Called by the handler before insert.
    /// </summary>
    Task<ProjectActual?> FindAsync(
        TenantId tenantId,
        ProjectId projectId,
        ActualSourceKind sourceKind,
        Guid? sourceRefId,
        CancellationToken cancellationToken = default);

    /// <summary>Append a freshly-projected row. Projector-only.</summary>
    Task InsertAsync(ProjectActual actual, CancellationToken cancellationToken = default);

    /// <summary>All non-tombstoned rows for a project.</summary>
    Task<IReadOnlyList<ProjectActual>> GetByProjectAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken cancellationToken = default);

    /// <summary>All non-tombstoned rows for a project filtered by category.</summary>
    Task<IReadOnlyList<ProjectActual>> GetByCategoryAsync(
        TenantId tenantId,
        ProjectId projectId,
        BudgetCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum of <see cref="ProjectActual.PostedAmount"/> for a project,
    /// grouped by <see cref="BudgetCategory"/>. Currency-agnostic —
    /// callers must enforce single-currency semantics upstream.
    /// </summary>
    Task<IReadOnlyDictionary<BudgetCategory, decimal>> GetTotalsAsync(
        TenantId tenantId,
        ProjectId projectId,
        CancellationToken cancellationToken = default);
}

using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Read-side surface for <see cref="ProjectActual"/>. Safe to expose
/// to UI / reporting / query consumers. All reads enforce H5 tenant
/// isolation. Write operations live on
/// <see cref="IProjectActualWriter"/> so callers cannot accidentally
/// bypass the projector's idempotency contract.
/// </summary>
public interface IProjectActualReader
{
    /// <summary>
    /// Composite-key idempotency lookup. Returns the existing row if
    /// already projected for this <c>(projectId, sourceKind,
    /// sourceRefId, glAccountId)</c> tuple, else null. Called by the
    /// handler before insert; including <paramref name="glAccountId"/>
    /// in the key preserves per-line granularity when a single JE
    /// posts multiple lines to the same project on different accounts
    /// (e.g. Labor + Materials split).
    /// </summary>
    Task<ProjectActual?> FindAsync(
        TenantId tenantId,
        ProjectId projectId,
        ActualSourceKind sourceKind,
        Guid? sourceRefId,
        Guid? glAccountId,
        CancellationToken cancellationToken = default);

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

/// <summary>
/// Projector-only write surface for <see cref="ProjectActual"/>.
/// Resolve this interface from DI only inside the projector
/// composition root; arbitrary consumers should NOT request this.
/// </summary>
public interface IProjectActualWriter
{
    /// <summary>Append a freshly-projected row. Throws on duplicate id.</summary>
    Task InsertAsync(ProjectActual actual, CancellationToken cancellationToken = default);
}

/// <summary>
/// Convenience composite that satisfies both <see cref="IProjectActualReader"/>
/// and <see cref="IProjectActualWriter"/> — kept for the in-memory
/// implementation. Production hosts should split DI registration so
/// only the projector receives the writer.
/// </summary>
public interface IProjectActualRepository : IProjectActualReader, IProjectActualWriter
{
}

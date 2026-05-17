using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Revision-aware repository for <see cref="ProjectBudget"/> +
/// <see cref="ProjectBudgetLine"/> per Stage 02 §2.4. New revisions
/// auto-increment <see cref="ProjectBudget.RevisionNumber"/>; the
/// prior current revision is superseded atomically with the insert.
/// </summary>
public interface IProjectBudgetRepository
{
    /// <summary>Fetch a budget revision by id, or null when unknown.</summary>
    Task<ProjectBudget?> GetAsync(
        ProjectBudgetId id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Return the current (non-superseded, non-deleted) revision for
    /// the project, or null when no revision has been written yet.
    /// </summary>
    Task<ProjectBudget?> GetCurrentAsync(
        ProjectId projectId,
        CancellationToken cancellationToken = default);

    /// <summary>List all revisions for the project in chronological (RevisionNumber ascending) order.</summary>
    Task<IReadOnlyList<ProjectBudget>> GetRevisionsAsync(
        ProjectId projectId,
        CancellationToken cancellationToken = default);

    /// <summary>List the lines belonging to a budget revision.</summary>
    Task<IReadOnlyList<ProjectBudgetLine>> GetLinesAsync(
        ProjectBudgetId budgetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert a new revision atomically with its lines + supersede
    /// the prior current revision (sets <c>EffectiveUntil</c> +
    /// <c>SupersededAt</c>). Auto-derives <c>RevisionNumber</c>.
    /// </summary>
    /// <exception cref="OverlappingBudgetRevisionException">
    /// Thrown when <paramref name="effectiveFrom"/> is &lt;= the prior
    /// current revision's <c>EffectiveFrom</c>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lines"/> has duplicate Category or is empty.</exception>
    Task<ProjectBudget> InsertRevisionAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateOnly effectiveFrom,
        IReadOnlyCollection<ProjectBudgetLineDraft> lines,
        Guid createdBy,
        Instant createdAt,
        string? notes = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when a new budget revision's <c>EffectiveFrom</c> overlaps
/// or precedes the prior current revision's date.
/// </summary>
public sealed class OverlappingBudgetRevisionException : InvalidOperationException
{
    public ProjectId ProjectId { get; }
    public DateOnly PriorEffectiveFrom { get; }
    public DateOnly AttemptedEffectiveFrom { get; }

    public OverlappingBudgetRevisionException(
        ProjectId projectId,
        DateOnly priorEffectiveFrom,
        DateOnly attemptedEffectiveFrom)
        : base($"Cannot insert ProjectBudget revision for project {projectId.Value} "
              + $"with EffectiveFrom {attemptedEffectiveFrom:O} — prior revision's "
              + $"EffectiveFrom is {priorEffectiveFrom:O}. New revision's EffectiveFrom "
              + "must be strictly after the prior revision's.")
    {
        ProjectId              = projectId;
        PriorEffectiveFrom     = priorEffectiveFrom;
        AttemptedEffectiveFrom = attemptedEffectiveFrom;
    }
}

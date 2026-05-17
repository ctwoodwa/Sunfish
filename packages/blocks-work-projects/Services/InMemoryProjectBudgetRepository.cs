using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// In-memory <see cref="IProjectBudgetRepository"/> for tests +
/// kitchen-sink demos. Production hosts wire a SQLite-backed
/// implementation in a follow-on persistence hand-off.
/// </summary>
public sealed class InMemoryProjectBudgetRepository : IProjectBudgetRepository
{
    private readonly ConcurrentDictionary<ProjectBudgetId, ProjectBudget> _headers = new();
    private readonly ConcurrentDictionary<ProjectBudgetId, IReadOnlyList<ProjectBudgetLine>> _lines = new();
    private readonly object _writeLock = new();

    /// <inheritdoc />
    public Task<ProjectBudget?> GetAsync(ProjectBudgetId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_headers.TryGetValue(id, out var h) && h.DeletedAt is null ? h : null);

    /// <inheritdoc />
    public Task<ProjectBudget?> GetCurrentAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        var current = _headers.Values
            .Where(h => h.ProjectId.Equals(projectId)
                        && h.SupersededAt is null
                        && h.DeletedAt is null)
            .OrderByDescending(h => h.RevisionNumber)
            .FirstOrDefault();
        return Task.FromResult(current);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectBudget>> GetRevisionsAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProjectBudget> rows = _headers.Values
            .Where(h => h.ProjectId.Equals(projectId) && h.DeletedAt is null)
            .OrderBy(h => h.RevisionNumber)
            .ToList();
        return Task.FromResult(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectBudgetLine>> GetLinesAsync(ProjectBudgetId budgetId, CancellationToken cancellationToken = default)
        => Task.FromResult(_lines.TryGetValue(budgetId, out var lines)
            ? lines
            : (IReadOnlyList<ProjectBudgetLine>)Array.Empty<ProjectBudgetLine>());

    /// <inheritdoc />
    public Task<ProjectBudget> InsertRevisionAsync(
        TenantId tenantId,
        ProjectId projectId,
        DateOnly effectiveFrom,
        IReadOnlyCollection<ProjectBudgetLineDraft> lines,
        Guid createdBy,
        Instant createdAt,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            throw new ArgumentException("At least one line is required.", nameof(lines));

        // Category uniqueness within the revision.
        var dupCategories = lines.GroupBy(l => l.Category).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupCategories.Count > 0)
            throw new ArgumentException(
                $"Duplicate categories in revision: {string.Join(", ", dupCategories)}. "
                + "Each Category may appear at most once per revision.",
                nameof(lines));

        lock (_writeLock)
        {
            // Determine prior current revision (if any) + auto-increment number.
            var prior = _headers.Values
                .Where(h => h.ProjectId.Equals(projectId)
                            && h.SupersededAt is null
                            && h.DeletedAt is null)
                .OrderByDescending(h => h.RevisionNumber)
                .FirstOrDefault();

            if (prior is not null && effectiveFrom <= prior.EffectiveFrom)
                throw new OverlappingBudgetRevisionException(projectId, prior.EffectiveFrom, effectiveFrom);

            var newRevisionNumber = (prior?.RevisionNumber ?? 0) + 1;
            var header = ProjectBudget.Create(
                tenantId:       tenantId,
                id:             ProjectBudgetId.NewId(),
                projectId:      projectId,
                revisionNumber: newRevisionNumber,
                effectiveFrom:  effectiveFrom,
                createdBy:      createdBy,
                createdAt:      createdAt,
                notes:          notes);

            // Build lines (validates per-line invariants like > 0 amount).
            var builtLines = lines
                .Select(d => ProjectBudgetLine.Create(
                    tenantId:       tenantId,
                    id:             ProjectBudgetLineId.NewId(),
                    budgetId:       header.Id,
                    category:       d.Category,
                    budgetedAmount: d.BudgetedAmount,
                    currency:       d.Currency,
                    createdBy:      createdBy,
                    createdAt:      createdAt,
                    glAccountId:    d.GlAccountId,
                    notes:          d.Notes))
                .ToList();

            // Atomic — supersede prior + insert new header + insert lines.
            // Inside the write-lock so a concurrent InsertRevisionAsync
            // can't race with our supersede.
            if (prior is not null)
            {
                // EffectiveUntil = day before new EffectiveFrom (inclusive
                // ranges; revisions are non-overlapping).
                prior.Supersede(effectiveFrom.AddDays(-1), createdAt);
            }
            _headers[header.Id] = header;
            _lines[header.Id]   = builtLines;
            return Task.FromResult(header);
        }
    }
}

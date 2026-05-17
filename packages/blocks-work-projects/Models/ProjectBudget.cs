using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Header for a <see cref="Project"/> budget revision per Stage 02
/// §2.4. <b>Append-only:</b> once a revision is written, only
/// <see cref="EffectiveUntil"/> + <see cref="SupersededAt"/> mutate
/// when a higher revision lands. Treat as posted-then-immutable per
/// <c>crdt-friendly-schema-conventions.md</c> §6.
/// </summary>
public sealed class ProjectBudget
{
    public ProjectBudgetId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }

    /// <summary>1-based per project; new revision = new row.</summary>
    public int RevisionNumber { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Null while this is the current revision; set when a higher revision supersedes.</summary>
    public DateOnly? EffectiveUntil { get; private set; }

    public string? Notes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    /// <summary>Set by the repository when a higher revision lands.</summary>
    public Instant? SupersededAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    private ProjectBudget(
        ProjectBudgetId id,
        TenantId tenantId,
        ProjectId projectId,
        int revisionNumber,
        DateOnly effectiveFrom,
        Guid createdBy,
        Instant createdAt,
        string? notes)
    {
        Id             = id;
        TenantId       = tenantId;
        ProjectId      = projectId;
        RevisionNumber = revisionNumber;
        EffectiveFrom  = effectiveFrom;
        Notes          = notes;
        CreatedBy      = createdBy;
        CreatedAt      = createdAt;
    }

    /// <summary>
    /// Build a new <see cref="ProjectBudget"/>. Caller (repository)
    /// supplies the auto-incremented <paramref name="revisionNumber"/>
    /// to keep budget creation a single atomic header+lines operation.
    /// </summary>
    public static ProjectBudget Create(
        TenantId tenantId,
        ProjectBudgetId id,
        ProjectId projectId,
        int revisionNumber,
        DateOnly effectiveFrom,
        Guid createdBy,
        Instant createdAt,
        string? notes = null)
    {
        if (revisionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "RevisionNumber must be >= 1.");
        return new ProjectBudget(id, tenantId, projectId, revisionNumber, effectiveFrom, createdBy, createdAt, notes);
    }

    /// <summary>
    /// Mark this revision as superseded — sets
    /// <see cref="SupersededAt"/> + <see cref="EffectiveUntil"/>.
    /// Internal so only the repository can advance the supersede
    /// chain.
    /// </summary>
    internal void Supersede(DateOnly until, Instant supersededAt)
    {
        if (SupersededAt is not null)
            throw new InvalidOperationException(
                $"ProjectBudget revision {RevisionNumber} (id={Id.Value}) is already superseded.");
        EffectiveUntil = until;
        SupersededAt   = supersededAt;
    }
}

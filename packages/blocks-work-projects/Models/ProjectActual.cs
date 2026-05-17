using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Materialized projection of cluster-financial activity attributable
/// to a single <see cref="ProjectId"/>, per Stage 02 §2.22. Sourced
/// event-style from <c>Financial.JournalEntryPosted</c> (filtered by
/// <c>dimensions["projectId"]</c>) by
/// <see cref="Services.IProjectActualProjector"/>. Append-only +
/// rebuildable from the upstream event stream per
/// <c>cross-cluster-event-bus-design.md</c> §5 — the table is derived
/// state, not source-of-truth. <see cref="DeletedAt"/> is a tombstone
/// for the (deferred) <c>Financial.JournalEntryReversed</c> case; this
/// PR does not set it.
/// </summary>
public sealed class ProjectActual
{
    public ProjectActualId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }
    public BudgetCategory Category { get; private set; }
    public Guid? GlAccountId { get; private set; }
    public decimal PostedAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateOnly PostedDate { get; private set; }
    public ActualSourceKind SourceKind { get; private set; }
    public Guid? SourceRefId { get; private set; }
    public string? Notes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }

    /// <summary>Max length of <see cref="Notes"/> — DoS + PII-smuggling guard at the model boundary.</summary>
    public const int MaxNotesLength = 4000;

    private ProjectActual() { }

    /// <summary>
    /// Build a new <see cref="ProjectActual"/>. Constructed by
    /// the projector — never by user code.
    /// </summary>
    public static ProjectActual Create(
        TenantId tenantId,
        ProjectActualId id,
        ProjectId projectId,
        BudgetCategory category,
        Guid? glAccountId,
        decimal postedAmount,
        string currency,
        DateOnly postedDate,
        ActualSourceKind sourceKind,
        Guid? sourceRefId,
        Instant createdAt,
        Guid createdBy,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsAsciiLetterUpper))
            throw new ArgumentException(
                $"Currency '{currency}' is not a 3-letter ISO-4217 code.", nameof(currency));
        if (notes is { Length: > MaxNotesLength })
            throw new ArgumentException(
                $"Notes exceeds MaxNotesLength={MaxNotesLength}.", nameof(notes));

        return new ProjectActual
        {
            Id           = id,
            TenantId     = tenantId,
            ProjectId    = projectId,
            Category     = category,
            GlAccountId  = glAccountId,
            PostedAmount = postedAmount,
            Currency     = normalizedCurrency,
            PostedDate   = postedDate,
            SourceKind   = sourceKind,
            SourceRefId  = sourceRefId,
            Notes        = notes,
            CreatedAt    = createdAt,
            CreatedBy    = createdBy,
        };
    }

    /// <summary>
    /// Tombstone — invoked by the projector when the upstream JE is
    /// reversed. Reserved for a follow-on hand-off; not used in this PR.
    /// </summary>
    internal void Tombstone(Instant at)
    {
        DeletedAt = at;
    }
}

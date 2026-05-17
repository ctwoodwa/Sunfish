using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Per-category line on a <see cref="ProjectBudget"/> revision per
/// Stage 02 §2.4. Unique on
/// <c>(<see cref="BudgetId"/>, <see cref="Category"/>)</c> — a
/// revision cannot have two rows for the same category.
/// </summary>
public sealed class ProjectBudgetLine
{
    public ProjectBudgetLineId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectBudgetId BudgetId { get; private set; }
    public BudgetCategory Category { get; private set; }

    /// <summary>FK to <c>blocks-financial-ledger.GLAccount</c> (loose; no nav).</summary>
    public Guid? GlAccountId { get; private set; }

    public decimal BudgetedAmount { get; private set; }
    public string Currency { get; private set; }
    public string? Notes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private ProjectBudgetLine(
        ProjectBudgetLineId id,
        TenantId tenantId,
        ProjectBudgetId budgetId,
        BudgetCategory category,
        decimal budgetedAmount,
        string currency,
        Guid? glAccountId,
        string? notes,
        Guid createdBy,
        Instant createdAt)
    {
        Id             = id;
        TenantId       = tenantId;
        BudgetId       = budgetId;
        Category       = category;
        BudgetedAmount = budgetedAmount;
        Currency       = currency;
        GlAccountId    = glAccountId;
        Notes          = notes;
        CreatedBy      = createdBy;
        CreatedAt      = createdAt;
    }

    public static ProjectBudgetLine Create(
        TenantId tenantId,
        ProjectBudgetLineId id,
        ProjectBudgetId budgetId,
        BudgetCategory category,
        decimal budgetedAmount,
        string currency,
        Guid createdBy,
        Instant createdAt,
        Guid? glAccountId = null,
        string? notes = null)
    {
        if (budgetedAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(budgetedAmount), "BudgetedAmount must be > 0.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException(
                "Currency must be a 3-letter ISO 4217 code (e.g., USD).",
                nameof(currency));
        return new ProjectBudgetLine(
            id, tenantId, budgetId, category, budgetedAmount,
            currency.ToUpperInvariant(), glAccountId, notes, createdBy, createdAt);
    }
}

/// <summary>
/// Caller-supplied draft used by
/// <see cref="Services.IProjectBudgetRepository.InsertRevisionAsync"/>
/// to construct lines atomically with the header.
/// </summary>
public sealed record ProjectBudgetLineDraft(
    BudgetCategory Category,
    decimal BudgetedAmount,
    string Currency,
    Guid? GlAccountId = null,
    string? Notes = null);

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Sub-entity of <see cref="RemodelProject"/> per Stage 02 §2.8.
/// <see cref="Ordinal"/> is 1-based + unique per
/// <see cref="RemodelProjectId"/> (service-layer enforced — uniqueness
/// is a cross-row invariant the entity cannot self-check).
/// </summary>
public sealed class RemodelPhase
{
    public RemodelPhaseId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public RemodelProjectId RemodelProjectId { get; private set; }
    public int Ordinal { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal BudgetedAmount { get; private set; }
    public string BudgetedCurrency { get; private set; } = "USD";
    public decimal? ActualAmount { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public PhaseStatus Status { get; private set; }

    /// <summary>Max length of <see cref="Name"/> — input-cap guard.</summary>
    public const int MaxNameLength = 200;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public long Version { get; private set; }

    private RemodelPhase() { }

    public static RemodelPhase Create(
        TenantId tenantId,
        RemodelPhaseId id,
        RemodelProjectId remodelProjectId,
        int ordinal,
        string name,
        decimal budgetedAmount,
        string budgetedCurrency,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        Guid createdBy,
        Instant createdAt)
    {
        if (ordinal < 1)
            throw new ArgumentException("Ordinal must be >= 1.", nameof(ordinal));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Length > MaxNameLength)
            throw new ArgumentException(
                $"Name exceeds MaxNameLength={MaxNameLength}.", nameof(name));
        if (budgetedAmount < 0m)
            throw new ArgumentException("BudgetedAmount must be >= 0.", nameof(budgetedAmount));
        if (budgetedAmount > RemodelProject.MaxCapitalizedAmount)
            throw new ArgumentException(
                $"BudgetedAmount exceeds sanity ceiling {RemodelProject.MaxCapitalizedAmount}.", nameof(budgetedAmount));
        var currency = NormalizeCurrency(budgetedCurrency, nameof(budgetedCurrency));
        if (plannedEndDate is { } end && plannedStartDate is { } start && end < start)
            throw new ArgumentException("PlannedEndDate must be >= PlannedStartDate.", nameof(plannedEndDate));

        return new RemodelPhase
        {
            Id               = id,
            TenantId         = tenantId,
            RemodelProjectId = remodelProjectId,
            Ordinal          = ordinal,
            Name             = name,
            BudgetedAmount   = budgetedAmount,
            BudgetedCurrency = currency,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate   = plannedEndDate,
            Status           = PhaseStatus.Planned,
            CreatedAt        = createdAt,
            UpdatedAt        = createdAt,
            CreatedBy        = createdBy,
            UpdatedBy        = createdBy,
        };
    }

    public void Start(DateOnly startDate, Guid updatedBy, Instant updatedAt)
    {
        if (Status != PhaseStatus.Planned)
            throw new InvalidOperationException($"Cannot Start a phase in status {Status}.");
        Status          = PhaseStatus.Active;
        ActualStartDate = startDate;
        UpdatedBy       = updatedBy;
        UpdatedAt       = updatedAt;
        Version        += 1;
    }

    public void Complete(DateOnly endDate, decimal? actualAmount, Guid updatedBy, Instant updatedAt)
    {
        if (Status != PhaseStatus.Active)
            throw new InvalidOperationException($"Cannot Complete a phase in status {Status}.");
        if (actualAmount is { } amt && amt < 0m)
            throw new ArgumentException("ActualAmount must be >= 0.", nameof(actualAmount));
        if (ActualStartDate is { } start && endDate < start)
            throw new ArgumentException("ActualEndDate must be >= ActualStartDate.", nameof(endDate));
        Status        = PhaseStatus.Complete;
        ActualEndDate = endDate;
        ActualAmount  = actualAmount;
        UpdatedBy     = updatedBy;
        UpdatedAt     = updatedAt;
        Version      += 1;
    }

    public void MarkOverBudget(decimal actualAmount, Guid updatedBy, Instant updatedAt)
    {
        // Only Active phases can be over-budget — a Planned phase has
        // no ActualStartDate, so "over budget on work that never
        // started" is meaningless. Pre-start re-budgeting is a separate
        // concern (out of scope for this PR).
        if (Status != PhaseStatus.Active)
            throw new InvalidOperationException($"Cannot MarkOverBudget a phase in status {Status}.");
        if (actualAmount < BudgetedAmount)
            throw new ArgumentException(
                "ActualAmount must exceed BudgetedAmount to mark over-budget.", nameof(actualAmount));
        if (actualAmount > RemodelProject.MaxCapitalizedAmount)
            throw new ArgumentException(
                $"ActualAmount exceeds sanity ceiling {RemodelProject.MaxCapitalizedAmount}.", nameof(actualAmount));
        Status       = PhaseStatus.OverBudget;
        ActualAmount = actualAmount;
        UpdatedBy    = updatedBy;
        UpdatedAt    = updatedAt;
        Version     += 1;
    }

    public void Cancel(Guid updatedBy, Instant updatedAt)
    {
        if (Status == PhaseStatus.Complete || Status == PhaseStatus.Cancelled)
            throw new InvalidOperationException($"Cannot Cancel a phase in terminal status {Status}.");
        Status    = PhaseStatus.Cancelled;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
        Version  += 1;
    }

    internal static string NormalizeCurrency(string currency, string paramName)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", paramName);
        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
            throw new ArgumentException(
                $"Currency '{currency}' is not a 3-letter ISO-4217 code.", paramName);
        return normalized;
    }
}

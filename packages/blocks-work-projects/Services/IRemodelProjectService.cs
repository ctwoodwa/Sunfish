using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Service surface for <see cref="RemodelProject"/> + child
/// <see cref="RemodelPhase"/> entities. Emits
/// <c>Work.RemodelPhaseCompleted</c> on phase completion and
/// <c>Work.RemodelCapitalized</c> on capitalization. The latter is the
/// cross-cluster trigger for <c>blocks-financial-ledger</c> to post the
/// capital-asset <c>JournalEntry</c>.
/// </summary>
/// <remarks>
/// AUTHORIZATION CONTRACT: Authorization is the caller's responsibility.
/// This service emits financial-cluster trigger events; callers MUST
/// verify the <c>updatedBy</c> principal holds the Accountant or Manager
/// role before invoking <see cref="CapitalizeAsync"/> or
/// <see cref="MarkPhaseCompleteAsync"/>. The service intentionally does
/// not consult <c>IUserContext</c> — authorization gates live at the
/// API boundary, not the service layer.
///
/// ACCOUNT VALIDATION CONTRACT: The financial account passed to
/// <see cref="CapitalizeAsync"/> as <c>capitalizationAccountId</c> is
/// not validated against the financial cluster's chart of accounts —
/// callers MUST verify the account exists + is a valid
/// capital/CIP-asset account before invocation. Eventual-consistency
/// drift (work-projects "capitalized" but financial reactor rejects)
/// is one-shot at the entity level; recovery requires manual
/// intervention.
/// </remarks>
public interface IRemodelProjectService
{
    Task<RemodelProject> CreateAsync(
        TenantId tenantId,
        ProjectId projectId,
        string scopeStatement,
        RemodelKind remodelKind,
        bool permitRequired,
        Guid createdBy,
        IReadOnlyList<string>? inspectionsRequired = null,
        CancellationToken cancellationToken = default);

    Task<RemodelPhase> AddPhaseAsync(
        TenantId tenantId,
        RemodelProjectId remodelProjectId,
        int ordinal,
        string name,
        decimal budgetedAmount,
        string budgetedCurrency,
        Guid createdBy,
        DateOnly? plannedStartDate = null,
        DateOnly? plannedEndDate = null,
        CancellationToken cancellationToken = default);

    Task<RemodelPhase> StartPhaseAsync(
        TenantId tenantId,
        RemodelPhaseId phaseId,
        DateOnly startDate,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition a phase to <see cref="PhaseStatus.Complete"/> and
    /// emit <c>Work.RemodelPhaseCompleted</c>.
    /// </summary>
    Task<RemodelPhase> MarkPhaseCompleteAsync(
        TenantId tenantId,
        RemodelPhaseId phaseId,
        DateOnly endDate,
        decimal? actualAmount,
        string? actualCurrency,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capitalize the project — invariant: every phase must be
    /// <see cref="PhaseStatus.Complete"/>, <see cref="PhaseStatus.OverBudget"/>,
    /// or <see cref="PhaseStatus.Cancelled"/>. Emits
    /// <c>Work.RemodelCapitalized</c> on success.
    /// </summary>
    /// <exception cref="RemodelHasIncompletePhasesException">
    /// Thrown when any phase is still in <c>Planned</c> or <c>Active</c>.
    /// </exception>
    Task<RemodelProject> CapitalizeAsync(
        TenantId tenantId,
        RemodelProjectId remodelProjectId,
        Guid capitalizationAccountId,
        DateOnly placedInServiceAt,
        decimal capitalizedAmount,
        string currency,
        Guid updatedBy,
        Guid? propertyId = null,
        CancellationToken cancellationToken = default);

    Task<RemodelProject?> GetByIdAsync(
        TenantId tenantId,
        RemodelProjectId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemodelPhase>> GetPhasesAsync(
        TenantId tenantId,
        RemodelProjectId remodelProjectId,
        CancellationToken cancellationToken = default);
}

using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Financial;

/// <summary>
/// Event payload emitted when a fiscal period transitions from
/// <see cref="FiscalPeriodStatus.Open"/> to
/// <see cref="FiscalPeriodStatus.SoftClosed"/> via the period-close
/// service per Stage 02 §6.5(a).
/// </summary>
/// <remarks>
/// Canonical event-type name: <c>Financial.PeriodSoftClosed</c> (matches
/// the existing entry in
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §3.1).
/// </remarks>
/// <param name="PeriodId">FK to the soft-closed period.</param>
/// <param name="ChartId">FK to the owning chart of accounts.</param>
/// <param name="ClosedByPrincipalId">Principal that performed the soft-close; null when not wired through an <c>IUserContext</c>.</param>
public sealed record PeriodSoftClosed(
    FiscalPeriodId PeriodId,
    ChartOfAccountsId ChartId,
    string? ClosedByPrincipalId);

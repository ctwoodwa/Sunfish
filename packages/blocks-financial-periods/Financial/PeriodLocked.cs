using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Financial;

/// <summary>
/// Event payload emitted when a fiscal period transitions to
/// <see cref="FiscalPeriodStatus.Locked"/> via
/// <c>IPeriodCloseService.LockAsync</c> — either ad-hoc admin locking
/// or as a step of year-end close
/// (<c>IFiscalYearCloseService.CloseFiscalYearAsync</c>, PR 3b).
/// </summary>
/// <remarks>
/// Canonical event-type name: <c>Financial.PeriodLocked</c>. Idempotency
/// key form: <c>$"period-locked:{PeriodId.Value}"</c>. Matches the
/// existing entry in
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §3.1
/// (hard-close event).
/// </remarks>
/// <param name="PeriodId">FK to the locked period.</param>
/// <param name="ChartId">FK to the owning chart of accounts.</param>
public sealed record PeriodLocked(
    FiscalPeriodId PeriodId,
    ChartOfAccountsId ChartId);

using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Financial;

/// <summary>
/// Event payload emitted when a fiscal period transitions to the
/// <see cref="FiscalPeriodStatus.Open"/> state — either freshly created
/// (<see cref="Reason"/> = <c>null</c>) or reopened from
/// <see cref="FiscalPeriodStatus.SoftClosed"/> by an admin
/// (<see cref="Reason"/> contains the audit memo).
/// </summary>
/// <remarks>
/// <para>
/// Canonical event-type name: <c>Financial.PeriodOpened</c>. Idempotency
/// key form: <c>$"period-opened:{PeriodId.Value}:{recordedAtUtc:O}"</c>.
/// </para>
/// <para>
/// New event in the cross-cluster event-bus catalog
/// (<c>_shared/engineering/cross-cluster-event-bus-design.md</c> §3.1).
/// Catalog entry added in PR 3 of the periods hand-off; PR 2 emits the
/// type from <see cref="Services.PeriodCloseService"/>.
/// </para>
/// </remarks>
/// <param name="PeriodId">FK to the opened period.</param>
/// <param name="ChartId">FK to the owning chart of accounts.</param>
/// <param name="Reason">Null when freshly opened; reopen audit memo when reopened by admin.</param>
public sealed record PeriodOpened(
    FiscalPeriodId PeriodId,
    ChartOfAccountsId ChartId,
    string? Reason);

using System.Collections.Generic;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.Reports.Cartridges.TrialBalance;

/// <summary>
/// Result of a Trial Balance run. Implements
/// <see cref="IReportProvisionalityCarrier"/> so the
/// <see cref="ReportRunner"/> propagates
/// <see cref="IsProvisional"/> + <see cref="Warnings"/> into the
/// <see cref="ReportRunResult{T}"/> envelope.
/// </summary>
/// <param name="ChartId">The chart this run was scoped to.</param>
/// <param name="AsOf">The as-of date the cartridge resolved (either explicit or <c>FiscalPeriod.EndDate</c>).</param>
/// <param name="PeriodId">Non-null when the run was bound to a fiscal period.</param>
/// <param name="Rows">Trial-balance rows ordered ascending by <c>AccountCode</c> then <c>AccountId.ToString()</c>.</param>
/// <param name="TotalDebit">Sum of <see cref="TrialBalanceRow.DebitBalance"/> across <c>Rows</c>.</param>
/// <param name="TotalCredit">Sum of <see cref="TrialBalanceRow.CreditBalance"/> across <c>Rows</c>.</param>
/// <param name="IsBalanced"><c>TotalDebit == TotalCredit</c>.</param>
/// <param name="IsProvisional"><c>true</c> when the bound fiscal period status is <c>Open</c> or <c>SoftClosed</c>.</param>
/// <param name="Warnings">Cartridge-attached warnings (provisionality reasons + unbalanced-chart diagnostic).</param>
public sealed record TrialBalanceResult(
    ChartOfAccountsId ChartId,
    System.DateOnly AsOf,
    FiscalPeriodId? PeriodId,
    IReadOnlyList<TrialBalanceRow> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool IsProvisional,
    IReadOnlyList<string> Warnings) : IReportProvisionalityCarrier;

/// <summary>One row of a <see cref="TrialBalanceResult"/>.</summary>
/// <param name="AccountId">Account identifier.</param>
/// <param name="AccountCode">Human-readable code (e.g. <c>"4100"</c>).</param>
/// <param name="AccountName">Display name (e.g. <c>"Rental Revenue"</c>).</param>
/// <param name="AccountType">Account category.</param>
/// <param name="DebitBalance">Balance projected onto the debit side. <c>0m</c> when the balance projects onto credit side.</param>
/// <param name="CreditBalance">Balance projected onto the credit side. <c>0m</c> when the balance projects onto debit side.</param>
public sealed record TrialBalanceRow(
    GLAccountId AccountId,
    string AccountCode,
    string AccountName,
    GLAccountType AccountType,
    decimal DebitBalance,
    decimal CreditBalance);

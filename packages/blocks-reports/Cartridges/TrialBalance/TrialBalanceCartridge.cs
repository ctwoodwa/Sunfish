using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Blocks.Reports.Exceptions;

namespace Sunfish.Blocks.Reports.Cartridges.TrialBalance;

/// <summary>
/// W#72 PR 2 — Trial Balance cartridge per Stage 02 §4.2. Reads
/// signed account balances via
/// <see cref="IGeneralLedgerReadModel"/>, enumerates the chart's
/// accounts via the widened
/// <see cref="IAccountResolver.EnumerateForChartAsync"/>, and
/// projects each balance onto the debit / credit column based on
/// the account's <see cref="NormalBalance"/> (derived from
/// <see cref="GLAccountType"/> when null per xo-ruling-T14-50Z D7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant isolation.</b> Per xo-ruling-T14-50Z D4 (Option C), the
/// caller (UI / Bridge endpoint) is responsible for resolving
/// <c>ChartId → LegalEntity → TenantId</c> and rejecting mismatched
/// tenants BEFORE calling the cartridge. The cartridge validates
/// only chart-existence + <see cref="GLAccount.IsActive"/>; it does
/// NOT (cannot) verify tenant scope because <see cref="ChartOfAccounts"/>
/// does not currently carry a <c>TenantId</c> field.
/// </para>
/// <para>
/// <b>Provisionality.</b> When bound to a
/// <see cref="FiscalPeriod"/>, the cartridge reports
/// <c>IsProvisional</c> = <c>true</c> for
/// <see cref="FiscalPeriodStatus.Open"/> and
/// <see cref="FiscalPeriodStatus.SoftClosed"/>. Explicit
/// <c>AsOfDate</c> always reports <c>IsProvisional</c> =
/// <c>false</c> (caller takes responsibility).
/// </para>
/// </remarks>
public sealed class TrialBalanceCartridge : IReportCartridge<TrialBalanceParameters, TrialBalanceResult>
{
    private readonly IChartRepository _charts;
    private readonly IAccountResolver _accounts;
    private readonly IFiscalPeriodRepository _periods;
    private readonly IGeneralLedgerReadModel _ledger;

    /// <summary>Construct bound to the four upstream cluster surfaces.</summary>
    public TrialBalanceCartridge(
        IChartRepository charts,
        IAccountResolver accounts,
        IFiscalPeriodRepository periods,
        IGeneralLedgerReadModel ledger)
    {
        _charts = charts ?? throw new ArgumentNullException(nameof(charts));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _periods = periods ?? throw new ArgumentNullException(nameof(periods));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    /// <inheritdoc />
    public ReportKind Kind => ReportKind.TrialBalance;

    /// <inheritdoc />
    public async Task<TrialBalanceResult> ExecuteAsync(
        ReportExecutionContext context,
        TrialBalanceParameters parameters,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // 1. Parameter validation — exactly one of (FiscalPeriodId, AsOfDate).
        if (parameters.FiscalPeriodId is null && parameters.AsOfDate is null)
            throw new ReportParameterValidationException(
                nameof(parameters),
                "TrialBalance requires either FiscalPeriodId or AsOfDate.");
        if (parameters.FiscalPeriodId is not null && parameters.AsOfDate is not null)
            throw new ReportParameterValidationException(
                nameof(parameters),
                "TrialBalance accepts FiscalPeriodId OR AsOfDate, not both.");

        // 2. Chart existence (tenant scoping is caller's responsibility per D4-C).
        var chart = await _charts.GetAsync(parameters.ChartId, ct).ConfigureAwait(false);
        if (chart is null)
            throw new ReportParameterValidationException(
                nameof(parameters.ChartId),
                $"ChartId {parameters.ChartId} not found.");

        // 3. Resolve as-of date + provisionality.
        var warnings = new List<string>();
        var isProvisional = false;
        System.DateOnly asOf;
        if (parameters.FiscalPeriodId is not null)
        {
            var period = await _periods.GetAsync(parameters.FiscalPeriodId.Value, ct).ConfigureAwait(false);
            if (period is null)
                throw new ReportParameterValidationException(
                    nameof(parameters.FiscalPeriodId),
                    $"FiscalPeriodId {parameters.FiscalPeriodId.Value} not found.");
            if (period.ChartId != parameters.ChartId)
                throw new ReportParameterValidationException(
                    nameof(parameters.FiscalPeriodId),
                    $"FiscalPeriodId {parameters.FiscalPeriodId.Value} belongs to a different chart ({period.ChartId}) than the requested ChartId ({parameters.ChartId}).");
            asOf = period.EndDate;
            if (period.Status != FiscalPeriodStatus.Locked)
            {
                isProvisional = true;
                warnings.Add($"Period {period.Label} is {period.Status}; values may shift on close.");
            }
        }
        else
        {
            asOf = parameters.AsOfDate!.Value;
        }

        // 4. Enumerate accounts (with active/inactive filter).
        var accounts = await _accounts.EnumerateForChartAsync(
            parameters.ChartId,
            includeInactive: parameters.IncludeInactiveAccounts,
            ct).ConfigureAwait(false);

        // 5. Read raw balances (signed: debit positive, credit negative per IGeneralLedgerReadModel contract).
        var balances = await _ledger.GetAccountBalancesAsOfAsync(
            parameters.ChartId, asOf, context.SnapshotMarker, ct).ConfigureAwait(false);

        // 6. Compose rows ordered by Code (ordinal) then Id (stable tie-break).
        var rows = new List<TrialBalanceRow>();
        decimal totalDebit = 0m, totalCredit = 0m;
        foreach (var account in accounts
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .ThenBy(a => a.Id.ToString(), StringComparer.Ordinal))
        {
            balances.TryGetValue(account.Id, out var raw);
            if (raw == 0m && !parameters.IncludeZeroBalanceAccounts) continue;

            var normalBalance = account.NormalBalance ?? DeriveNormalBalanceFromType(account.Type);
            var (debit, credit) = ProjectToSides(normalBalance, raw);
            rows.Add(new TrialBalanceRow(
                AccountId: account.Id,
                AccountCode: account.Code,
                AccountName: account.Name,
                AccountType: account.Type,
                DebitBalance: debit,
                CreditBalance: credit));
            totalDebit += debit;
            totalCredit += credit;
        }

        var isBalanced = totalDebit == totalCredit;
        if (!isBalanced)
            warnings.Add($"Chart is unbalanced: Debit {totalDebit:N2} != Credit {totalCredit:N2}.");

        return new TrialBalanceResult(
            ChartId: parameters.ChartId,
            AsOf: asOf,
            PeriodId: parameters.FiscalPeriodId,
            Rows: rows,
            TotalDebit: totalDebit,
            TotalCredit: totalCredit,
            IsBalanced: isBalanced,
            IsProvisional: isProvisional,
            Warnings: warnings);
    }

    // Per xo-ruling-T14-50Z D7 — NormalBalance defaults derived from GLAccountType
    // when account.NormalBalance is null. Matches the GLAccount.Create factory's
    // derivation rule.
    private static NormalBalance DeriveNormalBalanceFromType(GLAccountType type) => type switch
    {
        GLAccountType.Asset or GLAccountType.Expense => NormalBalance.Debit,
        _ => NormalBalance.Credit,   // Liability / Equity / Revenue
    };

    private static (decimal debit, decimal credit) ProjectToSides(NormalBalance side, decimal raw)
    {
        // raw is signed (debit positive). Project per the account's normal side:
        // - Debit-normal account, raw >= 0 → debit column shows raw, credit shows 0
        // - Debit-normal account, raw < 0 → debit shows 0, credit shows |raw| (unusual)
        // - Credit-normal account, raw <= 0 → debit shows 0, credit shows |raw|
        // - Credit-normal account, raw > 0 → debit shows raw, credit shows 0 (unusual)
        return side switch
        {
            NormalBalance.Debit when raw >= 0 => (raw, 0m),
            NormalBalance.Debit when raw < 0 => (0m, -raw),
            NormalBalance.Credit when raw <= 0 => (0m, -raw),
            NormalBalance.Credit when raw > 0 => (raw, 0m),
            _ => (0m, 0m),
        };
    }
}

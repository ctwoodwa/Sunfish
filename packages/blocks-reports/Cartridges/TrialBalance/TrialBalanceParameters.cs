using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.Reports.Cartridges.TrialBalance;

/// <summary>
/// Parameters for the Trial Balance cartridge. Exactly one of
/// <see cref="FiscalPeriodId"/> or <see cref="AsOfDate"/> must be
/// set — never both, never neither (parameter validation throws
/// <see cref="Exceptions.ReportParameterValidationException"/>).
/// </summary>
public sealed record TrialBalanceParameters
{
    /// <summary>Required. Identifies the chart whose accounts are reported.</summary>
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>Bind a fiscal period — cartridge uses <c>FiscalPeriod.EndDate</c> as the as-of date and reports provisionality from <c>FiscalPeriod.Status</c>.</summary>
    public FiscalPeriodId? FiscalPeriodId { get; init; }

    /// <summary>Explicit as-of date — caller takes responsibility for provisionality decisions.</summary>
    public System.DateOnly? AsOfDate { get; init; }

    /// <summary>When <c>true</c>, accounts with zero balance are included. Default <c>false</c>.</summary>
    public bool IncludeZeroBalanceAccounts { get; init; } = false;

    /// <summary>When <c>true</c>, accounts with <c>GLAccount.IsActive</c> = <c>false</c> are included. Default <c>false</c>. Renamed from the hand-off's <c>IncludeDeletedAccounts</c> per xo-ruling-T14-50Z D6 (soft-delete on <c>GLAccount</c> is <c>IsActive</c>, not <c>IsTombstoned</c>).</summary>
    public bool IncludeInactiveAccounts { get; init; } = false;
}

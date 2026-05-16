using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// A chart of accounts is the top-level container that groups all
/// <see cref="GLAccount"/>s for a single legal entity / fiscal year
/// configuration. Per Stage 02 <c>blocks-financial-schema-design.md</c>
/// §3.2.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="LegalEntityId">FK to the legal entity that owns this chart.</param>
/// <param name="Name">Display name, e.g. <c>"Acero Properties LLC — Operating"</c>.</param>
/// <param name="BaseCurrency">ISO 4217 currency code, e.g. <c>"USD"</c>.</param>
/// <param name="FiscalYearStartMonth">1..12 — month the fiscal year begins.</param>
/// <param name="FiscalYearStartDay">1..31 — day-of-month the fiscal year begins.</param>
/// <param name="RetainedEarningsAccountId">
/// Optional FK to the equity account where period-close transfers
/// accumulated net income. Null until the chart is wired against a
/// retained-earnings account.
/// </param>
/// <param name="IsActive">Soft-delete flag.</param>
/// <param name="CreatedAtUtc">Creation timestamp.</param>
/// <param name="UpdatedAtUtc">Last-mutation timestamp.</param>
public sealed record ChartOfAccounts(
    ChartOfAccountsId Id,
    LegalEntityId LegalEntityId,
    string Name,
    string BaseCurrency,
    int FiscalYearStartMonth,
    int FiscalYearStartDay,
    GLAccountId? RetainedEarningsAccountId,
    bool IsActive,
    Instant CreatedAtUtc,
    Instant UpdatedAtUtc);

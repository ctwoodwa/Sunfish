using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Filter accounts by chart + <see cref="GLAccountType"/> — used by
/// year-end close to enumerate the Revenue + Expense accounts that
/// need to be zeroed into retained earnings.
/// </summary>
/// <remarks>
/// Kept separate from the ledger's <c>IAccountResolver</c>
/// (account-by-id) to avoid widening the ledger's existing resolver
/// surface; sibling ledger or a follow-on hand-off may consolidate.
/// </remarks>
public interface IAccountTypeQuery
{
    /// <summary>
    /// Return all postable accounts of the supplied <paramref name="type"/>
    /// belonging to the supplied chart. Order is not guaranteed.
    /// </summary>
    Task<IReadOnlyList<GLAccount>> GetByTypeAsync(
        ChartOfAccountsId chartId,
        GLAccountType type,
        CancellationToken cancellationToken = default);
}

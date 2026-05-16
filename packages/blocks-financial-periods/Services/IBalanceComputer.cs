using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Computes a GL-account balance as-of a calendar date — sum of all
/// Posted journal-entry-line debits − credits whose entry date is
/// ≤ the cutoff. Used by year-end close to derive the retained-
/// earnings rollover targets per Stage 02 §6.5(b).
/// </summary>
/// <remarks>
/// <para>
/// <b>Local interface, ledger-domain semantics.</b> Lives in periods
/// because year-end close is the only current consumer; sibling ledger
/// or kernel-ledger may surface a canonical balance-projection later
/// (see <c>Sunfish.Kernel.Ledger.CQRS.IBalanceProjection</c> for the
/// CQRS read-side prior art at a different abstraction level). When a
/// canonical surface is ratified, this interface migrates to the
/// canonical home + the periods package re-namespaces consumers.
/// </para>
/// <para>
/// <b>Sign convention:</b> returns the signed running balance
/// (<c>Σdebits − Σcredits</c>). Revenue accounts typically carry a
/// credit balance and therefore return a negative number; Expense
/// accounts typically carry a debit balance and return positive.
/// The year-end close routine interprets the sign per
/// <see cref="GLAccount.NormalBalance"/>.
/// </para>
/// </remarks>
public interface IBalanceComputer
{
    /// <summary>
    /// Compute the as-of balance for the supplied account.
    /// </summary>
    /// <param name="accountId">The GL account to balance.</param>
    /// <param name="asOfDate">Inclusive cutoff date (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<decimal> ComputeAsOfAsync(
        GLAccountId accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);
}

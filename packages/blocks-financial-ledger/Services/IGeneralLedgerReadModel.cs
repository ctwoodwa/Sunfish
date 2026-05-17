using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Read-side projection over <see cref="IJournalStore"/> that returns
/// per-account closing balances as of a caller-supplied date. The
/// primary consumer is the Trial Balance cartridge in
/// <c>Sunfish.Blocks.Reports</c>; other read-side consumers
/// (AR/AP Aging, P&amp;L) compose their own projections on top of
/// <see cref="IJournalStore"/> directly.
/// </summary>
/// <remarks>
/// <para>
/// Returned values are <b>signed raw balances</b> (debit total minus
/// credit total per <see cref="GLAccountId"/>). The caller is
/// responsible for projecting onto debit-side / credit-side columns
/// based on each account's normal balance side — this surface
/// deliberately does NOT carry account-type or normal-side
/// information so cartridges can compose their own normalization.
/// </para>
/// <para>
/// <b>Read-side discipline.</b> Implementations MUST NOT mutate
/// state, emit events, or invoke any write-capable repository — they
/// project over the immutable journal log.
/// </para>
/// <para>
/// <b>Posted-only.</b> Only <see cref="JournalEntryStatus.Posted"/>
/// entries contribute to the balance. <see cref="JournalEntryStatus.Draft"/>
/// and <see cref="JournalEntryStatus.Reversed"/> entries are
/// excluded.
/// </para>
/// <para>
/// <b>Snapshot marker.</b> The opaque marker is forwarded from the
/// caller's report-runner context; Phase 1 implementations ignore it
/// (the in-memory store has no per-tenant snapshot isolation). When
/// per-cluster snapshot honor lands, implementations bind reads to
/// the marker without changing this contract.
/// </para>
/// </remarks>
public interface IGeneralLedgerReadModel
{
    /// <summary>
    /// Compute signed raw balances (debit - credit) per
    /// <see cref="GLAccountId"/> for the given chart, considering only
    /// posted entries with <see cref="JournalEntry.EntryDate"/> on or
    /// before <paramref name="asOf"/>.
    /// </summary>
    /// <param name="chartId">Chart of accounts to scope the projection to. Required.</param>
    /// <param name="asOf">Cutoff date (inclusive). Entries posted on or before this date contribute.</param>
    /// <param name="snapshotMarker">Opaque snapshot marker forwarded from the report-runner; Phase 1 implementations ignore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary keyed by <see cref="GLAccountId"/> with signed balances. Accounts with no posted activity are omitted.</returns>
    Task<IReadOnlyDictionary<GLAccountId, decimal>> GetAccountBalancesAsOfAsync(
        ChartOfAccountsId chartId,
        System.DateOnly asOf,
        string snapshotMarker,
        CancellationToken cancellationToken = default);
}

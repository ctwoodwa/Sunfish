using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// In-memory <see cref="IGeneralLedgerReadModel"/> backed by
/// <see cref="IJournalStore.Snapshot"/>. Computes signed balances by
/// scanning the snapshot + summing per-account
/// (<see cref="JournalEntryLine.Debit"/> - <see cref="JournalEntryLine.Credit"/>)
/// across all
/// <see cref="JournalEntryStatus.Posted"/> entries whose
/// <see cref="JournalEntry.EntryDate"/> is on or before the
/// caller-supplied as-of date and whose
/// <see cref="JournalEntry.ChartId"/> matches the requested chart.
/// </summary>
/// <remarks>
/// Suitable for Phase 1 (small tenant, in-memory journal). A SQLite-
/// backed implementation lands in a follow-on hand-off when the
/// journal store moves off in-memory; this contract is stable so the
/// swap is host-only.
/// </remarks>
public sealed class InMemoryGeneralLedgerReadModel : IGeneralLedgerReadModel
{
    private readonly IJournalStore _journals;

    /// <summary>Construct bound to a journal store.</summary>
    public InMemoryGeneralLedgerReadModel(IJournalStore journals)
        => _journals = journals ?? throw new System.ArgumentNullException(nameof(journals));

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<GLAccountId, decimal>> GetAccountBalancesAsOfAsync(
        ChartOfAccountsId chartId,
        System.DateOnly asOf,
        string snapshotMarker,
        CancellationToken cancellationToken = default)
    {
        // snapshotMarker accepted + ignored in Phase 1 (no per-tenant snapshot isolation in the in-memory store).
        var balances = new Dictionary<GLAccountId, decimal>();
        foreach (var entry in _journals.Snapshot())
        {
            if (entry.Status != JournalEntryStatus.Posted) continue;
            if (entry.ChartId is null || entry.ChartId.Value != chartId) continue;
            if (entry.EntryDate > asOf) continue;

            foreach (var line in entry.Lines)
            {
                if (!balances.TryGetValue(line.AccountId, out var running)) running = 0m;
                balances[line.AccountId] = running + line.Debit - line.Credit;
            }
        }
        return Task.FromResult<IReadOnlyDictionary<GLAccountId, decimal>>(balances);
    }
}

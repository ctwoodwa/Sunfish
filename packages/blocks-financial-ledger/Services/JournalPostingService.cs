using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Default <see cref="IJournalPostingService"/> implementing the Stage 02
/// §6.1 six-phase algorithm:
///
///   1. Preconditions — must be <see cref="JournalEntryStatus.Draft"/>
///      with ≥2 lines.
///   2. Balance — defense-in-depth re-check of Σ debit == Σ credit
///      (the <see cref="JournalEntry"/> constructor also enforces).
///   3. Account validity — each line's account exists, belongs to the
///      entry's chart (when set), and is postable.
///   4. Period gating — fiscal period for the entry date must exist and
///      be Open (or SoftClosed with FinancialAdmin role bypass).
///   5. Atomic commit — promote to Posted + persist via
///      <see cref="IJournalStore.SaveAtomicAsync"/>; the store rolls
///      back any partial writes on exception.
///   6. Result — <see cref="PostResult"/> with the promoted entry on
///      success; structured <see cref="PostError"/> on validation
///      failure (no exception thrown).
/// </summary>
public sealed class JournalPostingService : IJournalPostingService
{
    private const string FinancialAdminRole = "FinancialAdmin";

    private readonly IAccountResolver _accounts;
    private readonly IPeriodResolver _periods;
    private readonly IJournalStore _store;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public JournalPostingService(
        IAccountResolver accounts,
        IPeriodResolver periods,
        IJournalStore store,
        IUserContext user,
        TimeProvider time)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _periods  = periods  ?? throw new ArgumentNullException(nameof(periods));
        _store    = store    ?? throw new ArgumentNullException(nameof(store));
        _user     = user     ?? throw new ArgumentNullException(nameof(user));
        _time     = time     ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public async Task<PostResult> PostAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Phase 1 — preconditions.
        if (entry.Status != JournalEntryStatus.Draft)
            return new PostResult(null, PostError.NotADraft, $"status={entry.Status}");
        if (entry.Lines.Count < 2)
            return new PostResult(null, PostError.TooFewLines, $"lines={entry.Lines.Count}");

        // Phase 2 — balance (defense-in-depth; ctor enforces too).
        // Use decimal arithmetic — exact for currency amounts (Stage 02
        // §7 notes integer-minor-units is the schema canonical, but the
        // C# impl preserves decimal for back-compat; .NET decimal is
        // base-10 so no float roundoff).
        decimal debitSum  = 0m;
        decimal creditSum = 0m;
        foreach (var line in entry.Lines)
        {
            debitSum  += line.Debit;
            creditSum += line.Credit;
        }
        if (debitSum != creditSum)
        {
            return new PostResult(null, PostError.Imbalanced,
                $"debits={debitSum:F2}, credits={creditSum:F2}");
        }

        // Phase 3 — account validity.
        foreach (var line in entry.Lines)
        {
            var acct = await _accounts.GetAsync(line.AccountId, cancellationToken).ConfigureAwait(false);
            if (acct is null)
                return new PostResult(null, PostError.UnknownAccount, line.AccountId.Value);
            if (entry.ChartId is { } entryChart && acct.ChartId is { } acctChart && !acctChart.Equals(entryChart))
                return new PostResult(null, PostError.WrongChart,
                    $"account.ChartId={acctChart}, entry.ChartId={entryChart}");
            if (!acct.IsPostable)
                return new PostResult(null, PostError.AccountNotPostable, line.AccountId.Value);
        }

        // Phase 4 — period gating (only when the entry has a chart;
        // unscoped entries skip this).
        if (entry.ChartId is { } chartId)
        {
            var period = await _periods.ResolveAsync(chartId, entry.EntryDate, cancellationToken)
                .ConfigureAwait(false);
            if (period is not { } snapshot)
                return new PostResult(null, PostError.NoPeriodForDate,
                    $"chartId={chartId}, date={entry.EntryDate}");
            if (snapshot.Status == IPeriodResolver.Status.Locked)
                return new PostResult(null, PostError.PeriodLocked, $"periodId={snapshot.PeriodId}");
            if (snapshot.Status == IPeriodResolver.Status.SoftClosed && !_user.HasRole(FinancialAdminRole))
                return new PostResult(null, PostError.PeriodSoftClosed,
                    $"periodId={snapshot.PeriodId}, userId={_user.UserId}");
        }

        // Phase 5 — atomic commit.
        var posted = entry with
        {
            Status = JournalEntryStatus.Posted,
            PostedAtUtc = new Instant(_time.GetUtcNow()),
        };
        await _store.SaveAtomicAsync(posted, cancellationToken).ConfigureAwait(false);

        // Phase 6 — result.
        return new PostResult(posted, PostError.None, null);
    }
}

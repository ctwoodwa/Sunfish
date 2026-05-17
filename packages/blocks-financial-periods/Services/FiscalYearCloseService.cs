using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Default <see cref="IFiscalYearCloseService"/> per Stage 02 §6.5(b).
/// </summary>
public sealed class FiscalYearCloseService : IFiscalYearCloseService
{
    private const int PayloadSchemaVersion = 1;

    private readonly IFiscalYearRepository _years;
    private readonly IFiscalPeriodRepository _periods;
    private readonly IPeriodCloseService _periodClose;
    private readonly IChartRepository _charts;
    private readonly IAccountTypeQuery _accountQuery;
    private readonly IBalanceComputer _balances;
    private readonly IJournalPostingService _posting;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly TenantId _tenantId;
    private readonly ReplicaId _replicaId;

    public FiscalYearCloseService(
        IFiscalYearRepository years,
        IFiscalPeriodRepository periods,
        IPeriodCloseService periodClose,
        IChartRepository charts,
        IAccountTypeQuery accountQuery,
        IBalanceComputer balances,
        IJournalPostingService posting,
        IDomainEventPublisher events,
        TimeProvider time,
        TenantId? tenantId = null,
        ReplicaId? replicaId = null)
    {
        _years        = years        ?? throw new ArgumentNullException(nameof(years));
        _periods      = periods      ?? throw new ArgumentNullException(nameof(periods));
        _periodClose  = periodClose  ?? throw new ArgumentNullException(nameof(periodClose));
        _charts       = charts       ?? throw new ArgumentNullException(nameof(charts));
        _accountQuery = accountQuery ?? throw new ArgumentNullException(nameof(accountQuery));
        _balances     = balances     ?? throw new ArgumentNullException(nameof(balances));
        _posting      = posting      ?? throw new ArgumentNullException(nameof(posting));
        _events       = events       ?? throw new ArgumentNullException(nameof(events));
        _time         = time         ?? throw new ArgumentNullException(nameof(time));
        _tenantId     = tenantId  ?? TenantId.System;
        _replicaId    = replicaId ?? ReplicaId.System;
    }

    /// <inheritdoc />
    public async Task<FiscalYearCloseResult> CloseFiscalYearAsync(
        FiscalYearId fyId,
        CancellationToken cancellationToken = default)
    {
        var fy = await _years.GetAsync(fyId, cancellationToken).ConfigureAwait(false);
        if (fy is null)
            return new FiscalYearCloseResult(null, null, FiscalYearCloseError.FiscalYearNotFound, fyId.Value);
        if (fy.Status == FiscalYearStatus.Closed)
            return new FiscalYearCloseResult(fy, null, FiscalYearCloseError.FiscalYearAlreadyClosed, null);

        var chart = await _charts.GetAsync(fy.ChartId, cancellationToken).ConfigureAwait(false);
        if (chart is null)
            return new FiscalYearCloseResult(fy, null, FiscalYearCloseError.ChartNotFound, fy.ChartId.Value);
        if (chart.RetainedEarningsAccountId is null)
            return new FiscalYearCloseResult(fy, null,
                FiscalYearCloseError.RetainedEarningsAccountNotConfigured,
                "Chart of accounts lacks a designated retained-earnings account.");

        // Step 1 — ensure all periods at least SoftClosed.
        var periods = await _periods.GetByFiscalYearAsync(fy.Id, cancellationToken).ConfigureAwait(false);
        foreach (var p in periods.Where(p => p.Status == FiscalPeriodStatus.Open))
        {
            var soft = await _periodClose.SoftCloseAsync(p.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!soft.IsSuccess)
                return new FiscalYearCloseResult(fy, null,
                    FiscalYearCloseError.ClosingJournalEntryFailed,
                    $"Auto-soft-close failed for period {p.Id.Value}: {soft.Error} {soft.Detail}");
        }

        // Refresh periods after the soft-close pass so subsequent lock
        // writes see the updated Version values.
        periods = await _periods.GetByFiscalYearAsync(fy.Id, cancellationToken).ConfigureAwait(false);

        // Step 2 — build the closing JE lines.
        var asOf = fy.EndDate;
        var incomeAccounts  = await _accountQuery.GetByTypeAsync(fy.ChartId, GLAccountType.Revenue, cancellationToken).ConfigureAwait(false);
        var expenseAccounts = await _accountQuery.GetByTypeAsync(fy.ChartId, GLAccountType.Expense, cancellationToken).ConfigureAwait(false);

        var closingLines = new List<JournalEntryLine>();
        decimal incomeNet = 0m, expenseNet = 0m;

        // Revenue accounts normally credit-balance — IBalanceComputer
        // returns Σdebit − Σcredit so a healthy revenue account
        // returns a negative number. Zero it by debit-ing the absolute
        // value.
        foreach (var acct in incomeAccounts)
        {
            var bal = await _balances.ComputeAsOfAsync(acct.Id, asOf, cancellationToken).ConfigureAwait(false);
            if (bal == 0m) continue;
            incomeNet += -bal;  // accumulate as positive revenue
            closingLines.Add(new JournalEntryLine(
                accountId: acct.Id,
                debit:  bal < 0m ? -bal : 0m,
                credit: bal > 0m ?  bal : 0m,
                notes: $"Year-end close to retained earnings — {fy.Label}"));
        }

        // Expense accounts normally debit-balance — IBalanceComputer
        // returns positive for a healthy expense account. Zero by
        // crediting the absolute value.
        foreach (var acct in expenseAccounts)
        {
            var bal = await _balances.ComputeAsOfAsync(acct.Id, asOf, cancellationToken).ConfigureAwait(false);
            if (bal == 0m) continue;
            expenseNet += bal;  // accumulate as positive expense
            closingLines.Add(new JournalEntryLine(
                accountId: acct.Id,
                debit:  bal < 0m ? -bal : 0m,
                credit: bal > 0m ?  bal : 0m,
                notes: $"Year-end close to retained earnings — {fy.Label}"));
        }

        var netIncome = incomeNet - expenseNet;

        // Add the retained-earnings rollover line to balance the JE.
        if (netIncome > 0m)
        {
            // Profit → credit retained earnings.
            closingLines.Add(new JournalEntryLine(
                accountId: chart.RetainedEarningsAccountId.Value,
                debit: 0m, credit: netIncome,
                notes: "Net income to retained earnings"));
        }
        else if (netIncome < 0m)
        {
            // Loss → debit retained earnings.
            closingLines.Add(new JournalEntryLine(
                accountId: chart.RetainedEarningsAccountId.Value,
                debit: -netIncome, credit: 0m,
                notes: "Net loss to retained earnings"));
        }

        // Step 3 — build + post the closing JE. Idempotency guard:
        // if a prior partial-failure already posted the closing JE
        // (fy.ClosingJournalEntryId non-null), reuse that id and skip
        // re-posting. This prevents the addendum-D2 retry path from
        // double-posting after a mid-step-4 lock failure.
        JournalEntryId? closingEntryId = fy.ClosingJournalEntryId;
        if (closingEntryId is null && closingLines.Count > 0)
        {
            var closingEntry = new JournalEntry(
                id: JournalEntryId.NewId(),
                entryDate: fy.EndDate,
                memo: $"Year-end closing entry — {fy.Label}",
                lines: closingLines,
                createdAtUtc: new Instant(_time.GetUtcNow()))
            {
                ChartId    = fy.ChartId,
                Status     = JournalEntryStatus.Draft,
                SourceKind = JournalEntrySource.Closing,
            };

            var postResult = await _posting.PostAsync(closingEntry, cancellationToken).ConfigureAwait(false);
            if (postResult.Error != PostError.None)
                return new FiscalYearCloseResult(fy, null,
                    FiscalYearCloseError.ClosingJournalEntryFailed,
                    $"{postResult.Error}: {postResult.Detail}");

            closingEntryId = postResult.Entry!.Id;
        }

        // Step 4 — lock all periods + flip FY status. Not transactional
        // yet (addendum D2 deferred); failures mid-loop leave the
        // ledger in a partially-locked state that the operator can
        // unlock via PeriodCloseService.UnlockAsync.
        foreach (var p in periods)
        {
            if (p.Status == FiscalPeriodStatus.Locked) continue;
            var lockResult = await _periodClose.LockAsync(p.Id, cancellationToken).ConfigureAwait(false);
            if (!lockResult.IsSuccess && lockResult.Error != PeriodCloseError.PeriodAlreadyLocked)
                return new FiscalYearCloseResult(fy, closingEntryId,
                    FiscalYearCloseError.ClosingJournalEntryFailed,
                    $"Lock failed for period {p.Id.Value}: {lockResult.Error} {lockResult.Detail}");
        }

        var now = new Instant(_time.GetUtcNow());
        var closedFy = fy with
        {
            Status                = FiscalYearStatus.Closed,
            ClosedAtUtc           = now,
            ClosingJournalEntryId = closingEntryId,
            Version               = fy.Version + 1,
        };
        if (!await _years.UpdateAsync(closedFy, cancellationToken).ConfigureAwait(false))
            return new FiscalYearCloseResult(fy, closingEntryId, FiscalYearCloseError.ConcurrentUpdate, null);

        // Step 5 — emit YearClosed + YearEndRolloverCompleted using the
        // §3.1 catalog idempotency-key format (`year-closed:{fyId}` /
        // `year-end-rollover:{fyId}`). Periods/Year events use the
        // catalog format directly; PeriodSoftClosed/PeriodOpened/
        // PeriodLocked carry the wider {eventType}|{tenant}|… format
        // pending XO ruling reconciliation per cob-question-22-25Z.
        await PublishAsync(
            "Financial.YearClosed",
            $"year-closed:{closedFy.Id.Value}",
            new YearClosed(
                FiscalYearId:          closedFy.Id,
                ChartId:               closedFy.ChartId,
                ClosingJournalEntryId: closingEntryId),
            cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            "Financial.YearEndRolloverCompleted",
            $"year-end-rollover:{closedFy.Id.Value}",
            new YearEndRolloverCompleted(
                FiscalYearId:          closedFy.Id,
                ChartId:               closedFy.ChartId,
                ClosingJournalEntryId: closingEntryId,
                NetIncome:             netIncome,
                IncomeAccountsClosed:  incomeAccounts.Count,
                ExpenseAccountsClosed: expenseAccounts.Count),
            cancellationToken).ConfigureAwait(false);

        return new FiscalYearCloseResult(closedFy, closingEntryId, FiscalYearCloseError.None, null);
    }

    /// <inheritdoc />
    public async Task<FiscalYearCloseResult> ReopenFiscalYearAsync(
        FiscalYearId fyId,
        string auditMemo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditMemo))
            return new FiscalYearCloseResult(null, null, FiscalYearCloseError.AuditMemoRequired, null);

        var fy = await _years.GetAsync(fyId, cancellationToken).ConfigureAwait(false);
        if (fy is null)
            return new FiscalYearCloseResult(null, null, FiscalYearCloseError.FiscalYearNotFound, fyId.Value);
        if (fy.Status == FiscalYearStatus.Open)
            return new FiscalYearCloseResult(fy, null, FiscalYearCloseError.FiscalYearAlreadyOpen, null);

        // Step 1 — if the closed FY has an associated closing JE,
        // reopen is GAAP-broken until we can post a reversal. The
        // reverse-by-id helper lands in PR 3d (needs sibling ledger
        // surface). Reject loudly with closingId in Detail so the
        // admin workflow can choose to (a) wait for PR 3d, or (b)
        // post a manual reversal then re-invoke reopen on a FY whose
        // ClosingJournalEntryId has been nulled.
        if (fy.ClosingJournalEntryId is { } closingId)
        {
            return new FiscalYearCloseResult(fy, closingId,
                FiscalYearCloseError.ReversalEntryFailed,
                $"Reopen requires reversal of closing JE {closingId.Value}; "
                + "the reverse-by-id helper ships in PR 3d (addendum). "
                + "Manual workaround: post the reversal entry, then null "
                + "FiscalYear.ClosingJournalEntryId, then re-invoke reopen.");
        }

        // Step 2 — flip FY → Open BEFORE unlocking periods so
        // PeriodCloseService.UnlockAsync's FY-Closed gate does not
        // reject every call. Partial-failure window: addendum D2
        // (non-transactional; failures leave FY=Open with some
        // periods still Locked — operator can re-invoke reopen).
        var periods = await _periods.GetByFiscalYearAsync(fy.Id, cancellationToken).ConfigureAwait(false);

        var now = new Instant(_time.GetUtcNow());
        var reopenedFy = fy with
        {
            Status                = FiscalYearStatus.Open,
            ClosedAtUtc           = null,
            ClosingJournalEntryId = null,
            Version               = fy.Version + 1,
        };
        if (!await _years.UpdateAsync(reopenedFy, cancellationToken).ConfigureAwait(false))
            return new FiscalYearCloseResult(fy, fy.ClosingJournalEntryId, FiscalYearCloseError.ConcurrentUpdate, null);

        // Now unlock all locked periods (the FY-Closed gate in UnlockAsync
        // no longer fires since we flipped above).
        foreach (var p in periods.Where(p => p.Status == FiscalPeriodStatus.Locked))
        {
            var unlock = await _periodClose.UnlockAsync(p.Id, $"FY reopen: {auditMemo}", cancellationToken).ConfigureAwait(false);
            if (!unlock.IsSuccess && unlock.Error != PeriodCloseError.PeriodNotLocked)
                return new FiscalYearCloseResult(reopenedFy, fy.ClosingJournalEntryId,
                    FiscalYearCloseError.ReversalEntryFailed,
                    $"Unlock failed for period {p.Id.Value}: {unlock.Error} {unlock.Detail}");
        }

        // Emit YearClosed-reverse signal as a YearClosed event with
        // ClosingJournalEntryId = null and the audit memo conveyed via
        // CorrelationId for trace. (The cross-cluster catalog does not
        // yet define Financial.YearReopened — addendum entry tracks
        // adding it in the foundation-events sweep PR.) Idempotency
        // key suffix `:reopen` distinguishes the reopen emission from
        // the original close emission for the same FY.
        await PublishAsync(
            "Financial.YearClosed",
            $"year-closed:{reopenedFy.Id.Value}:reopen",
            new YearClosed(
                FiscalYearId:          reopenedFy.Id,
                ChartId:               reopenedFy.ChartId,
                ClosingJournalEntryId: null),
            cancellationToken,
            correlationId: $"FY reopen: {auditMemo}").ConfigureAwait(false);

        return new FiscalYearCloseResult(reopenedFy, fy.ClosingJournalEntryId, FiscalYearCloseError.None, null);
    }

    private Task PublishAsync<TPayload>(
        string eventType,
        string idempotencyKey,
        TPayload payload,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        var envelope = new DomainEventEnvelope<TPayload>
        {
            EventId              = Guid.CreateVersion7().ToString(),
            EventType            = eventType,
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = _time.GetUtcNow(),
            TenantId             = _tenantId,
            OriginatingReplicaId = _replicaId,
            IdempotencyKey       = idempotencyKey,
            CorrelationId        = correlationId,
            Payload              = payload,
        };
        return _events.PublishAsync(envelope, cancellationToken);
    }
}

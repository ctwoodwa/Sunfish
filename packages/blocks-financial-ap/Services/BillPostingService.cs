using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Models.Events;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// Default <see cref="IBillPostingService"/>. Mirrors AR's
/// <c>InvoicePostingService</c> pattern; coordinates repository,
/// tax, ledger-posting, and event-publisher collaborators without
/// embedding domain logic in their constructors.
/// </summary>
public sealed class BillPostingService : IBillPostingService
{
    private readonly IBillRepository _bills;
    private readonly ITaxCalculator _tax;
    private readonly IJournalPostingService _journals;
    private readonly IDomainEventPublisher _events;

    public BillPostingService(
        IBillRepository bills,
        ITaxCalculator tax,
        IJournalPostingService journals,
        IDomainEventPublisher? events = null)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _tax = tax ?? throw new ArgumentNullException(nameof(tax));
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
        _events = events ?? new NoopDomainEventPublisher();
    }

    // ──────────────────────────────────────────────────────────────────
    //  RecordAsync — Draft → Received
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<RecordResult> RecordAsync(
        BillId billId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var bill = await _bills.GetAsync(billId, cancellationToken).ConfigureAwait(false);
        if (bill is null)
            return new RecordResult(null, null, RecordError.UnknownBill, $"Bill '{billId.Value}' does not exist or is tombstoned.");

        // Idempotent: re-recording an already-Received bill returns existing JE id.
        if (bill.Status == BillStatus.Received && bill.JournalEntryId is not null)
            return new RecordResult(bill, bill.JournalEntryId, RecordError.None, "Already recorded; no-op.");

        if (bill.Status != BillStatus.Draft)
            return new RecordResult(bill, null, RecordError.InvalidStatusForRecord,
                $"Cannot record bill in status '{bill.Status}' — only Draft is recordable.");

        if (bill.Lines.Count == 0)
            return new RecordResult(bill, null, RecordError.NoLines, "Bill has no lines.");

        // Compute per-line tax.
        var updatedLines = new List<BillLine>(bill.Lines.Count);
        decimal subtotal = 0m;
        decimal taxTotal = 0m;
        foreach (var line in bill.Lines)
        {
            var taxAmount = await _tax.CalculateAsync(line.TaxCodeId, line.Amount, bill.BillDate, cancellationToken)
                .ConfigureAwait(false);
            updatedLines.Add(line with { TaxAmount = taxAmount });
            subtotal += line.Amount;
            taxTotal += taxAmount;
        }
        var total = subtotal + taxTotal;

        // Build the journal entry. AP posts: Debit each line's account,
        // Credit AP for the total. Tax (when present) routes the same
        // zero-net placeholder pair as AR until the tax-bridge adapter
        // resolves real tax-payable accounts.
        var jeLines = new List<JournalEntryLine>(updatedLines.Count + 2);
        foreach (var line in updatedLines)
        {
            jeLines.Add(new JournalEntryLine(line.DebitAccountId, debit: line.Amount, credit: 0m));
        }
        jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: 0m, credit: total));
        if (taxTotal > 0m)
        {
            jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: taxTotal, credit: 0m));
            jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: 0m, credit: taxTotal));
        }

        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: bill.BillDate,
            memo: $"Bill {bill.BillNumber} from {bill.VendorId.Value}",
            lines: jeLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"bill:{bill.Id.Value}");

        var postResult = await _journals.PostAsync(entry, cancellationToken).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new RecordResult(bill, null, RecordError.JournalRejected, postResult.Detail);

        var now = Instant.Now;
        var recorded = bill with
        {
            Lines = updatedLines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            Balance = total,
            Status = BillStatus.Received,
            JournalEntryId = entry.Id,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(recorded, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsPayableEventNames.BillRecorded,
            new BillRecordedPayload(recorded.Id, recorded.BillNumber, recorded.VendorId, total, recorded.DueDate, recorded.PropertyId, entry.Id),
            $"bill-recorded:{recorded.Id.Value}",
            recorded.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new RecordResult(recorded, entry.Id, RecordError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  VoidAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VoidResult> VoidAsync(
        BillId billId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var bill = await _bills.GetAsync(billId, cancellationToken).ConfigureAwait(false);
        if (bill is null)
            return new VoidResult(null, null, VoidError.UnknownBill, $"Bill '{billId.Value}' does not exist or is tombstoned.");

        if (bill.Status is not (BillStatus.Received or BillStatus.Approved or BillStatus.PartiallyPaid))
            return new VoidResult(bill, null, VoidError.InvalidStatusForVoid,
                $"Cannot void bill in status '{bill.Status}'.");

        if (bill.JournalEntryId is null)
            return new VoidResult(bill, null, VoidError.NoJournalEntryToReverse, "Bill has no source journal entry to reverse.");

        // Build reversal: same accounts, debits/credits swapped.
        var jeLines = new List<JournalEntryLine>(bill.Lines.Count + 2);
        foreach (var line in bill.Lines)
        {
            jeLines.Add(new JournalEntryLine(line.DebitAccountId, debit: 0m, credit: line.Amount));
        }
        jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: bill.Total, credit: 0m));
        if (bill.TaxTotal > 0m)
        {
            jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: 0m, credit: bill.TaxTotal));
            jeLines.Add(new JournalEntryLine(bill.ApAccountId, debit: bill.TaxTotal, credit: 0m));
        }

        var reversal = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: $"Void bill {bill.BillNumber}: {reason}",
            lines: jeLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"bill-void:{bill.Id.Value}");

        var postResult = await _journals.PostAsync(reversal, cancellationToken).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new VoidResult(bill, null, VoidError.JournalRejected, postResult.Detail);

        var now = Instant.Now;
        var voided = bill with
        {
            Status = BillStatus.Voided,
            VoidedByEntryId = reversal.Id,
            Balance = 0m,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(voided, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsPayableEventNames.BillVoided,
            new BillVoidedPayload(voided.Id, voided.BillNumber, reversal.Id, reason),
            $"bill-voided:{voided.Id.Value}",
            voided.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new VoidResult(voided, reversal.Id, VoidError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ApproveAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ApproveResult> ApproveAsync(
        BillId billId,
        string approvedByUserId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approvedByUserId))
            return new ApproveResult(null, ApproveError.InvalidApproverId, "ApprovedByUserId is required.");

        var bill = await _bills.GetAsync(billId, cancellationToken).ConfigureAwait(false);
        if (bill is null)
            return new ApproveResult(null, ApproveError.UnknownBill, $"Bill '{billId.Value}' does not exist or is tombstoned.");

        if (bill.Status != BillStatus.Received)
            return new ApproveResult(bill, ApproveError.InvalidStatusForApproval,
                $"Cannot approve bill in status '{bill.Status}' — only Received is approvable.");

        var now = Instant.Now;
        var approved = bill with
        {
            Status = BillStatus.Approved,
            ApprovedByUserId = approvedByUserId,
            ApprovedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(approved, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsPayableEventNames.BillApproved,
            new BillApprovedPayload(approved.Id, approved.BillNumber, approvedByUserId),
            $"bill-approved:{approved.Id.Value}",
            approved.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new ApproveResult(approved, ApproveError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  DisputeAsync / ResolveDisputeAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DisputeResult> DisputeAsync(
        BillId billId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var bill = await _bills.GetAsync(billId, cancellationToken).ConfigureAwait(false);
        if (bill is null)
            return new DisputeResult(null, DisputeError.UnknownBill, $"Bill '{billId.Value}' does not exist or is tombstoned.");

        if (!bill.Status.IsPayable())
            return new DisputeResult(bill, DisputeError.InvalidStatusForDispute,
                $"Cannot place bill in status '{bill.Status}' on dispute hold — only Received / Approved / PartiallyPaid are disputable.");

        var now = Instant.Now;
        var disputed = bill with
        {
            Status = BillStatus.Disputed,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(disputed, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsPayableEventNames.BillDisputed,
            new BillDisputedPayload(disputed.Id, disputed.BillNumber, reason),
            $"bill-disputed:{disputed.Id.Value}",
            disputed.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new DisputeResult(disputed, DisputeError.None, null);
    }

    /// <inheritdoc />
    public async Task<ResolveDisputeResult> ResolveDisputeAsync(
        BillId billId,
        BillStatus resolveTo,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (resolveTo is not (BillStatus.Received or BillStatus.Approved))
            return new ResolveDisputeResult(null, ResolveDisputeError.InvalidResolutionTarget,
                $"Dispute may only resolve to Received or Approved (got '{resolveTo}').");

        var bill = await _bills.GetAsync(billId, cancellationToken).ConfigureAwait(false);
        if (bill is null)
            return new ResolveDisputeResult(null, ResolveDisputeError.UnknownBill, $"Bill '{billId.Value}' does not exist or is tombstoned.");

        if (bill.Status != BillStatus.Disputed)
            return new ResolveDisputeResult(bill, ResolveDisputeError.InvalidStatusForResolve,
                $"Cannot resolve dispute on bill in status '{bill.Status}' — only Disputed bills can be resolved.");

        var now = Instant.Now;
        var resolved = bill with
        {
            Status = resolveTo,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(resolved, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsPayableEventNames.DisputeResolved,
            new DisputeResolvedPayload(resolved.Id, resolved.BillNumber, resolveTo),
            $"dispute-resolved:{resolved.Id.Value}",
            resolved.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new ResolveDisputeResult(resolved, ResolveDisputeError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    private Task PublishAsync<TPayload>(
        string eventType,
        TPayload payload,
        string idempotencyKey,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var envelope = new DomainEventEnvelope<TPayload>
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey = idempotencyKey,
            Payload = payload!,
        };
        return _events.PublishAsync(envelope, cancellationToken);
    }
}

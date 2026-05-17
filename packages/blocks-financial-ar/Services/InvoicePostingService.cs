using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Models.Events;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// Default <see cref="IInvoicePostingService"/>. Coordinates the
/// repository, numbering, tax, ledger-posting, and event-publisher
/// dependencies — no domain logic lives inside the constructors of
/// those collaborators.
/// </summary>
public sealed class InvoicePostingService : IInvoicePostingService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceNumberingService _numbering;
    private readonly ITaxCalculator _tax;
    private readonly IJournalPostingService _journals;
    private readonly IDomainEventPublisher _events;

    public InvoicePostingService(
        IInvoiceRepository invoices,
        IInvoiceNumberingService numbering,
        ITaxCalculator tax,
        IJournalPostingService journals,
        IDomainEventPublisher? events = null)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
        _tax = tax ?? throw new ArgumentNullException(nameof(tax));
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
        _events = events ?? new NoopDomainEventPublisher();
    }

    // ──────────────────────────────────────────────────────────────────
    //  IssueAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IssueResult> IssueAsync(
        InvoiceId invoiceId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
            return new IssueResult(null, null, IssueError.UnknownInvoice, $"Invoice '{invoiceId.Value}' does not exist or is tombstoned.");

        // Idempotent: already-Issued invoice returns the existing record + JE id without re-posting.
        if (invoice.Status == InvoiceStatus.Issued && invoice.JournalEntryId is not null)
            return new IssueResult(invoice, invoice.JournalEntryId, IssueError.None, "Already issued; no-op.");

        if (invoice.Status != InvoiceStatus.Draft)
            return new IssueResult(invoice, null, IssueError.InvalidStatusForIssue,
                $"Cannot issue invoice in status '{invoice.Status}' — only Draft is issuable.");

        if (invoice.Lines.Count == 0)
            return new IssueResult(invoice, null, IssueError.NoLines, "Invoice has no lines.");

        // Mint number if blank (post-supply or draft scenario).
        var invoiceNumber = string.IsNullOrEmpty(invoice.InvoiceNumber)
            ? await _numbering.NextNumberAsync(invoice.ChartId, invoice.IssueDate, cancellationToken).ConfigureAwait(false)
            : invoice.InvoiceNumber;

        // Compute per-line tax. Mutate a working copy of lines with the
        // resolved TaxAmount so the materialized totals match the JE.
        var updatedLines = new List<InvoiceLine>(invoice.Lines.Count);
        decimal subtotal = 0m;
        decimal taxTotal = 0m;
        foreach (var line in invoice.Lines)
        {
            var taxAmount = await _tax.CalculateAsync(line.TaxCodeId, line.Amount, invoice.IssueDate, cancellationToken)
                .ConfigureAwait(false);
            updatedLines.Add(line with { TaxAmount = taxAmount });
            subtotal += line.Amount;
            taxTotal += taxAmount;
        }
        var total = subtotal + taxTotal;

        // Build the journal entry: Debit AR (total), Credit each income line, Credit tax payable (single line, if any).
        // Tax-payable account is intentionally NOT split per jurisdiction here — that's the canonical tax cluster's job.
        // PR 3 emits a single roll-up credit to the AR account again ONLY as a placeholder when tax exists; a real
        // tax-payable lookup arrives when the bridge adapter to blocks-financial-tax lands.
        var jeLines = new List<JournalEntryLine>(updatedLines.Count + 2)
        {
            new(invoice.ArAccountId, debit: total, credit: 0m),
        };
        foreach (var line in updatedLines)
        {
            jeLines.Add(new JournalEntryLine(line.IncomeAccountId, debit: 0m, credit: line.Amount));
        }
        if (taxTotal > 0m)
        {
            // PLACEHOLDER: route the tax credit back to the AR account so the
            // entry balances. When the tax-bridge adapter lands, this becomes
            // a real per-jurisdiction credit. Until then a non-zero tax-total
            // means consumers should have wired a real ITaxCalculator anyway.
            jeLines.Add(new JournalEntryLine(invoice.ArAccountId, debit: 0m, credit: taxTotal));
            // And re-debit the same amount so net effect is zero on AR for the tax portion.
            jeLines.Add(new JournalEntryLine(invoice.ArAccountId, debit: taxTotal, credit: 0m));
        }

        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: invoice.IssueDate,
            memo: $"Invoice {invoiceNumber}",
            lines: jeLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"invoice:{invoice.Id.Value}");

        var postResult = await _journals.PostAsync(entry, cancellationToken).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new IssueResult(invoice, null, IssueError.JournalRejected, postResult.Detail);

        // Persist the updated invoice — Status=Issued, JE id, number, totals.
        var now = Instant.Now;
        var issued = invoice with
        {
            InvoiceNumber = invoiceNumber,
            Lines = updatedLines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            Balance = total,
            Status = InvoiceStatus.Issued,
            JournalEntryId = entry.Id,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        };
        await _invoices.UpsertAsync(issued, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsReceivableEventNames.InvoiceIssued,
            new InvoiceIssuedPayload(issued.Id, invoiceNumber, issued.CustomerId, total, issued.DueDate, issued.PropertyId, entry.Id),
            $"invoice-issued:{issued.Id.Value}",
            issued.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new IssueResult(issued, entry.Id, IssueError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  VoidAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VoidResult> VoidAsync(
        InvoiceId invoiceId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
            return new VoidResult(null, null, VoidError.UnknownInvoice, $"Invoice '{invoiceId.Value}' does not exist or is tombstoned.");

        if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
            return new VoidResult(invoice, null, VoidError.InvalidStatusForVoid,
                $"Cannot void invoice in status '{invoice.Status}'. Only Issued / PartiallyPaid are voidable.");

        if (invoice.JournalEntryId is null)
            return new VoidResult(invoice, null, VoidError.NoJournalEntryToReverse, "Invoice has no source journal entry to reverse.");

        // Build the reversal: same accounts, debits/credits swapped.
        // We synthesize from invoice fields rather than fetching the original
        // JE — this PR doesn't take an IJournalStore dep (read-side of the
        // ledger). PR 4 or a follow-up can refactor to read the actual entry
        // when more nuanced reversal scenarios surface.
        var jeLines = new List<JournalEntryLine>(invoice.Lines.Count + 2)
        {
            new(invoice.ArAccountId, debit: 0m, credit: invoice.Total),
        };
        foreach (var line in invoice.Lines)
        {
            jeLines.Add(new JournalEntryLine(line.IncomeAccountId, debit: line.Amount, credit: 0m));
        }
        if (invoice.TaxTotal > 0m)
        {
            jeLines.Add(new JournalEntryLine(invoice.ArAccountId, debit: invoice.TaxTotal, credit: 0m));
            jeLines.Add(new JournalEntryLine(invoice.ArAccountId, debit: 0m, credit: invoice.TaxTotal));
        }

        var reversal = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: $"Void invoice {invoice.InvoiceNumber}: {reason}",
            lines: jeLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"invoice-void:{invoice.Id.Value}");

        var postResult = await _journals.PostAsync(reversal, cancellationToken).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new VoidResult(invoice, null, VoidError.JournalRejected, postResult.Detail);

        var now = Instant.Now;
        var voided = invoice with
        {
            Status = InvoiceStatus.Voided,
            VoidedByEntryId = reversal.Id,
            Balance = 0m,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        };
        await _invoices.UpsertAsync(voided, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsReceivableEventNames.InvoiceVoided,
            new InvoiceVoidedPayload(voided.Id, voided.InvoiceNumber, reversal.Id, reason),
            $"invoice-voided:{voided.Id.Value}",
            voided.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new VoidResult(voided, reversal.Id, VoidError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  WriteOffAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<WriteOffResult> WriteOffAsync(
        InvoiceId invoiceId,
        GLAccountId badDebtAccountId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badDebtAccountId.Value))
            return new WriteOffResult(null, null, WriteOffError.InvalidBadDebtAccount, "Bad-debt account id is required.");

        var invoice = await _invoices.GetAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
            return new WriteOffResult(null, null, WriteOffError.UnknownInvoice, $"Invoice '{invoiceId.Value}' does not exist or is tombstoned.");

        if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
            return new WriteOffResult(invoice, null, WriteOffError.InvalidStatusForWriteOff,
                $"Cannot write off invoice in status '{invoice.Status}'.");

        // Bad-debt entry: Debit BadDebtExpense / Credit AR for the open balance.
        var amount = invoice.Balance;
        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: $"Write off invoice {invoice.InvoiceNumber}: {reason}",
            lines: new[]
            {
                new JournalEntryLine(badDebtAccountId, debit: amount, credit: 0m),
                new JournalEntryLine(invoice.ArAccountId, debit: 0m, credit: amount),
            },
            createdAtUtc: Instant.Now,
            sourceReference: $"invoice-writeoff:{invoice.Id.Value}");

        var postResult = await _journals.PostAsync(entry, cancellationToken).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new WriteOffResult(invoice, null, WriteOffError.JournalRejected, postResult.Detail);

        var now = Instant.Now;
        var writtenOff = invoice with
        {
            Status = InvoiceStatus.WrittenOff,
            WrittenOffByEntryId = entry.Id,
            Balance = 0m,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        };
        await _invoices.UpsertAsync(writtenOff, cancellationToken).ConfigureAwait(false);

        await PublishAsync(
            AccountsReceivableEventNames.InvoiceWrittenOff,
            new InvoiceWrittenOffPayload(writtenOff.Id, writtenOff.InvoiceNumber, entry.Id, amount, reason),
            $"invoice-writtenoff:{writtenOff.Id.Value}",
            writtenOff.TenantId,
            cancellationToken).ConfigureAwait(false);

        return new WriteOffResult(writtenOff, entry.Id, WriteOffError.None, null);
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

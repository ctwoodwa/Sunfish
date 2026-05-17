using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// Posts AR invoices to the GL. Wraps <c>IJournalPostingService</c>
/// from <c>blocks-financial-ledger</c> with AR-specific concerns:
/// invoice-number minting on issue, per-line tax computation,
/// status transitions, and canonical event emission. Idempotent
/// where it can be: issuing an already-issued invoice returns the
/// same record + JE id without re-posting.
/// </summary>
public interface IInvoicePostingService
{
    /// <summary>
    /// Transition Draft → Issued. Mints the invoice number if blank,
    /// computes per-line tax, posts a balanced journal entry
    /// (Debit AR / Credit Income + Credit TaxPayable), updates the
    /// invoice with status/number/JE id, and emits
    /// <c>Financial.InvoiceIssued</c>.
    /// </summary>
    Task<IssueResult> IssueAsync(
        InvoiceId invoiceId,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Issued / PartiallyPaid → Voided. Posts a reversing
    /// journal entry (mirror of the issue entry with debit/credit
    /// swapped), updates the invoice, and emits
    /// <c>Financial.InvoiceVoided</c>.
    /// </summary>
    Task<VoidResult> VoidAsync(
        InvoiceId invoiceId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Issued / PartiallyPaid → WrittenOff. Posts a bad-debt
    /// journal entry (Debit BadDebtExpense / Credit AR), updates the
    /// invoice (Balance forced to 0), and emits
    /// <c>Financial.InvoiceWrittenOff</c>.
    /// </summary>
    Task<WriteOffResult> WriteOffAsync(
        InvoiceId invoiceId,
        GLAccountId badDebtAccountId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IInvoicePostingService.IssueAsync"/>.</summary>
public sealed record IssueResult(
    Invoice? Invoice,
    JournalEntryId? PostedEntryId,
    IssueError Error,
    string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == IssueError.None;
}

/// <summary>Outcome of <see cref="IInvoicePostingService.VoidAsync"/>.</summary>
public sealed record VoidResult(
    Invoice? Invoice,
    JournalEntryId? ReversalEntryId,
    VoidError Error,
    string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == VoidError.None;
}

/// <summary>Outcome of <see cref="IInvoicePostingService.WriteOffAsync"/>.</summary>
public sealed record WriteOffResult(
    Invoice? Invoice,
    JournalEntryId? BadDebtEntryId,
    WriteOffError Error,
    string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == WriteOffError.None;
}

/// <summary>Structured failure modes for <see cref="IInvoicePostingService.IssueAsync"/>.</summary>
public enum IssueError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The invoice id does not resolve to a live invoice.</summary>
    UnknownInvoice,
    /// <summary>The invoice is not in <see cref="InvoiceStatus.Draft"/> and was not already <see cref="InvoiceStatus.Issued"/> — illegal transition.</summary>
    InvalidStatusForIssue,
    /// <summary>The invoice has no lines; issuing zero would create a zero-total document with no audit trail.</summary>
    NoLines,
    /// <summary>The downstream <c>IJournalPostingService.PostAsync</c> rejected the entry (balance / period / account-validity).</summary>
    JournalRejected,
}

/// <summary>Structured failure modes for <see cref="IInvoicePostingService.VoidAsync"/>.</summary>
public enum VoidError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The invoice id does not resolve.</summary>
    UnknownInvoice,
    /// <summary>The invoice is not in a state that can be voided (Draft / Paid / Voided / WrittenOff).</summary>
    InvalidStatusForVoid,
    /// <summary>The invoice has no underlying journal entry to reverse (corrupted state).</summary>
    NoJournalEntryToReverse,
    /// <summary>The downstream <c>IJournalPostingService.PostAsync</c> rejected the reversal.</summary>
    JournalRejected,
}

/// <summary>Structured failure modes for <see cref="IInvoicePostingService.WriteOffAsync"/>.</summary>
public enum WriteOffError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The invoice id does not resolve.</summary>
    UnknownInvoice,
    /// <summary>The invoice is not in a state that can be written off (Draft / terminal).</summary>
    InvalidStatusForWriteOff,
    /// <summary>The bad-debt account id was empty.</summary>
    InvalidBadDebtAccount,
    /// <summary>The downstream <c>IJournalPostingService.PostAsync</c> rejected the entry.</summary>
    JournalRejected,
}

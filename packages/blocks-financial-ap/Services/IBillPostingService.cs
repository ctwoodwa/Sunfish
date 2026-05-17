using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// Coordinates AP lifecycle operations against the GL. Wraps
/// <c>IJournalPostingService</c> from <c>blocks-financial-ledger</c>
/// with AP-specific concerns: status transitions, per-line tax
/// computation, idempotent record/void, dispute-hold semantics, and
/// canonical event emission.
/// </summary>
public interface IBillPostingService
{
    /// <summary>
    /// Transition Draft → Received. Computes per-line tax, posts a
    /// balanced journal entry (Debit each line's Expense/Asset account,
    /// Credit AP for the total), updates the bill with status/JE id,
    /// and emits <c>Financial.BillRecorded</c>.
    /// Idempotent: re-recording an already-Received bill with a
    /// JournalEntryId returns the existing record without re-posting.
    /// </summary>
    Task<RecordResult> RecordAsync(
        BillId billId,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Received / Approved / PartiallyPaid → Voided. Posts a
    /// reversing journal entry and emits <c>Financial.BillVoided</c>.
    /// </summary>
    Task<VoidResult> VoidAsync(
        BillId billId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition Received → Approved. Stamps <c>ApprovedByUserId</c> +
    /// <c>ApprovedAtUtc</c>. Emits <c>Financial.BillApproved</c>.
    /// </summary>
    Task<ApproveResult> ApproveAsync(
        BillId billId,
        string approvedByUserId,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Place a bill on dispute hold (Received / Approved / PartiallyPaid →
    /// Disputed). Does NOT post a reversal — the GL is unchanged; the bill
    /// is just excluded from aging + payment-applicable queries.
    /// </summary>
    Task<DisputeResult> DisputeAsync(
        BillId billId,
        string reason,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a dispute back to a payable state (Disputed → Received or
    /// Approved). Caller chooses which state to resolve to.
    /// </summary>
    Task<ResolveDisputeResult> ResolveDisputeAsync(
        BillId billId,
        BillStatus resolveTo,
        PartyId actor,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IBillPostingService.RecordAsync"/>.</summary>
public sealed record RecordResult(Bill? Bill, JournalEntryId? PostedEntryId, RecordError Error, string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == RecordError.None;
}

/// <summary>Outcome of <see cref="IBillPostingService.VoidAsync"/>.</summary>
public sealed record VoidResult(Bill? Bill, JournalEntryId? ReversalEntryId, VoidError Error, string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == VoidError.None;
}

/// <summary>Outcome of <see cref="IBillPostingService.ApproveAsync"/>.</summary>
public sealed record ApproveResult(Bill? Bill, ApproveError Error, string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == ApproveError.None;
}

/// <summary>Outcome of <see cref="IBillPostingService.DisputeAsync"/>.</summary>
public sealed record DisputeResult(Bill? Bill, DisputeError Error, string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == DisputeError.None;
}

/// <summary>Outcome of <see cref="IBillPostingService.ResolveDisputeAsync"/>.</summary>
public sealed record ResolveDisputeResult(Bill? Bill, ResolveDisputeError Error, string? Detail)
{
    /// <summary>True iff the operation succeeded.</summary>
    public bool IsSuccess => Error == ResolveDisputeError.None;
}

/// <summary>Structured failure modes for <see cref="IBillPostingService.RecordAsync"/>.</summary>
public enum RecordError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The bill id does not resolve to a live bill.</summary>
    UnknownBill,
    /// <summary>The bill is not Draft and was not already Received — illegal transition.</summary>
    InvalidStatusForRecord,
    /// <summary>The bill has no lines.</summary>
    NoLines,
    /// <summary>The downstream <c>IJournalPostingService</c> rejected the entry.</summary>
    JournalRejected,
}

/// <summary>Structured failure modes for <see cref="IBillPostingService.VoidAsync"/>.</summary>
public enum VoidError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The bill id does not resolve.</summary>
    UnknownBill,
    /// <summary>The bill is not in a state that can be voided.</summary>
    InvalidStatusForVoid,
    /// <summary>The bill has no underlying journal entry to reverse.</summary>
    NoJournalEntryToReverse,
    /// <summary>The downstream <c>IJournalPostingService</c> rejected the reversal.</summary>
    JournalRejected,
}

/// <summary>Structured failure modes for <see cref="IBillPostingService.ApproveAsync"/>.</summary>
public enum ApproveError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The bill id does not resolve.</summary>
    UnknownBill,
    /// <summary>Only Received bills can be approved.</summary>
    InvalidStatusForApproval,
    /// <summary>ApprovedByUserId was empty.</summary>
    InvalidApproverId,
}

/// <summary>Structured failure modes for <see cref="IBillPostingService.DisputeAsync"/>.</summary>
public enum DisputeError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The bill id does not resolve.</summary>
    UnknownBill,
    /// <summary>The bill is not in a payable state (Received / Approved / PartiallyPaid).</summary>
    InvalidStatusForDispute,
}

/// <summary>Structured failure modes for <see cref="IBillPostingService.ResolveDisputeAsync"/>.</summary>
public enum ResolveDisputeError
{
    /// <summary>Success.</summary>
    None,
    /// <summary>The bill id does not resolve.</summary>
    UnknownBill,
    /// <summary>The bill is not currently in Disputed status.</summary>
    InvalidStatusForResolve,
    /// <summary>The caller asked to resolve to a non-allowed status (only Received and Approved are valid resolutions).</summary>
    InvalidResolutionTarget,
}

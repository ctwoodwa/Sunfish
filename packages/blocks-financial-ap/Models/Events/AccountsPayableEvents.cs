using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAp.Models.Events;

/// <summary>
/// Canonical event-type strings emitted by <c>BillPostingService</c> via
/// <see cref="Sunfish.Foundation.Events.IDomainEventPublisher"/>. Producer
/// cluster = <c>Financial</c> — same prefix as AR and Periods so a
/// downstream subscriber filtering on <c>"Financial.*"</c> picks up the
/// whole receivable + payable surface.
/// </summary>
public static class AccountsPayableEventNames
{
    /// <summary>A bill was recorded (Draft → Received, JE posted).</summary>
    public const string BillRecorded = "Financial.BillRecorded";

    /// <summary>A bill was voided.</summary>
    public const string BillVoided = "Financial.BillVoided";

    /// <summary>A bill was approved for payment.</summary>
    public const string BillApproved = "Financial.BillApproved";

    /// <summary>A bill was placed on dispute hold.</summary>
    public const string BillDisputed = "Financial.BillDisputed";

    /// <summary>A dispute was resolved — bill returns to a payable state.</summary>
    public const string DisputeResolved = "Financial.DisputeResolved";
}

/// <summary>Payload for <see cref="AccountsPayableEventNames.BillRecorded"/>.</summary>
public sealed record BillRecordedPayload(
    BillId BillId,
    string BillNumber,
    PartyId VendorId,
    decimal TotalAmount,
    DateOnly DueDate,
    string? PropertyId,
    JournalEntryId JournalEntryId);

/// <summary>Payload for <see cref="AccountsPayableEventNames.BillVoided"/>.</summary>
public sealed record BillVoidedPayload(
    BillId BillId,
    string BillNumber,
    JournalEntryId ReversalEntryId,
    string Reason);

/// <summary>Payload for <see cref="AccountsPayableEventNames.BillApproved"/>.</summary>
public sealed record BillApprovedPayload(
    BillId BillId,
    string BillNumber,
    string ApprovedByUserId);

/// <summary>Payload for <see cref="AccountsPayableEventNames.BillDisputed"/>.</summary>
public sealed record BillDisputedPayload(
    BillId BillId,
    string BillNumber,
    string Reason);

/// <summary>Payload for <see cref="AccountsPayableEventNames.DisputeResolved"/>.</summary>
public sealed record DisputeResolvedPayload(
    BillId BillId,
    string BillNumber,
    BillStatus ResolvedTo);

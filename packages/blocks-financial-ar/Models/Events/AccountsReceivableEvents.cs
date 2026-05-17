using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAr.Models.Events;

/// <summary>
/// Canonical event-type strings emitted by <c>InvoicePostingService</c> via
/// <see cref="Sunfish.Foundation.Events.IDomainEventPublisher"/>. Producer
/// cluster = <c>Financial</c> (mirrors blocks-financial-ledger /
/// blocks-financial-periods naming).
/// </summary>
public static class AccountsReceivableEventNames
{
    /// <summary>An invoice was issued (Draft → Issued).</summary>
    public const string InvoiceIssued = "Financial.InvoiceIssued";

    /// <summary>An invoice was voided.</summary>
    public const string InvoiceVoided = "Financial.InvoiceVoided";

    /// <summary>An invoice was written off as bad debt.</summary>
    public const string InvoiceWrittenOff = "Financial.InvoiceWrittenOff";
}

/// <summary>Payload for <see cref="AccountsReceivableEventNames.InvoiceIssued"/>.</summary>
public sealed record InvoiceIssuedPayload(
    InvoiceId InvoiceId,
    string InvoiceNumber,
    PartyId CustomerId,
    decimal TotalAmount,
    DateOnly DueDate,
    string? PropertyId,
    JournalEntryId JournalEntryId);

/// <summary>Payload for <see cref="AccountsReceivableEventNames.InvoiceVoided"/>.</summary>
public sealed record InvoiceVoidedPayload(
    InvoiceId InvoiceId,
    string InvoiceNumber,
    JournalEntryId ReversalEntryId,
    string Reason);

/// <summary>Payload for <see cref="AccountsReceivableEventNames.InvoiceWrittenOff"/>.</summary>
public sealed record InvoiceWrittenOffPayload(
    InvoiceId InvoiceId,
    string InvoiceNumber,
    JournalEntryId BadDebtEntryId,
    decimal AmountWrittenOff,
    string Reason);

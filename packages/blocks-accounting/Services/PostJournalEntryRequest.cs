using Sunfish.Blocks.Accounting.Models;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Input for <see cref="IAccountingService.PostEntryAsync"/>.
/// </summary>
/// <param name="EntryDate">Accounting date the entry is effective for.</param>
/// <param name="Memo">Human-readable description of the transaction.</param>
/// <param name="Lines">
/// Debit/credit lines. Must not be empty. Total debits must equal total credits.
/// All referenced <see cref="JournalEntryLine.AccountId"/>s must exist.
/// </param>
/// <param name="SourceReference">
/// Optional opaque back-reference to the originating event (e.g. <c>"rent-payment:INV-123"</c>).
/// </param>
public sealed record PostJournalEntryRequest(
    DateOnly EntryDate,
    string Memo,
    IReadOnlyList<JournalEntryLine> Lines,
    string? SourceReference = null);

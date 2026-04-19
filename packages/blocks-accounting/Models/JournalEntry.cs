using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// An immutable, double-entry accounting record consisting of one or more
/// <see cref="JournalEntryLine"/>s whose debits and credits balance.
/// </summary>
/// <remarks>
/// Invariant (enforced at construction): sum of all <see cref="JournalEntryLine.Debit"/> values
/// must equal sum of all <see cref="JournalEntryLine.Credit"/> values across all
/// <see cref="Lines"/>. Imbalanced entries are rejected with <see cref="ArgumentException"/>.
/// </remarks>
/// <param name="Id">Unique journal entry identifier.</param>
/// <param name="EntryDate">The accounting date this entry is effective for.</param>
/// <param name="Memo">Human-readable description of the transaction.</param>
/// <param name="Lines">
/// Ordered list of debit/credit lines. Must not be empty, and debits must equal credits.
/// </param>
/// <param name="CreatedAtUtc">Wall-clock instant at which the entry was posted.</param>
/// <param name="SourceReference">
/// Optional opaque string linking back to the originating event, e.g.
/// <c>"rent-payment:INV-123"</c>. Used for audit traceability; not interpreted by this package.
/// </param>
public sealed record JournalEntry
{
    /// <summary>Unique journal entry identifier.</summary>
    public JournalEntryId Id { get; }

    /// <summary>The accounting date this entry is effective for.</summary>
    public DateOnly EntryDate { get; }

    /// <summary>Human-readable description of the transaction.</summary>
    public string Memo { get; }

    /// <summary>
    /// Ordered, read-only list of debit/credit lines.
    /// Debits and credits are guaranteed to be balanced.
    /// </summary>
    public IReadOnlyList<JournalEntryLine> Lines { get; }

    /// <summary>Wall-clock instant at which the entry was posted.</summary>
    public Instant CreatedAtUtc { get; }

    /// <summary>
    /// Optional opaque reference to the originating event (e.g. <c>"rent-payment:INV-123"</c>).
    /// </summary>
    public string? SourceReference { get; }

    /// <summary>Constructs and validates a journal entry.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lines"/> is empty, or when total debits do not equal total credits.
    /// </exception>
    public JournalEntry(
        JournalEntryId id,
        DateOnly entryDate,
        string memo,
        IReadOnlyList<JournalEntryLine> lines,
        Instant createdAtUtc,
        string? sourceReference = null)
    {
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("A journal entry must have at least one line.", nameof(lines));

        var totalDebits = lines.Sum(l => l.Debit);
        var totalCredits = lines.Sum(l => l.Credit);
        if (totalDebits != totalCredits)
            throw new ArgumentException(
                $"Journal entry is imbalanced: total debits ({totalDebits:F2}) do not equal total credits ({totalCredits:F2}).",
                nameof(lines));

        Id = id;
        EntryDate = entryDate;
        Memo = memo;
        Lines = lines;
        CreatedAtUtc = createdAtUtc;
        SourceReference = sourceReference;
    }
}

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// An immutable, double-entry accounting record consisting of one or more
/// <see cref="JournalEntryLine"/>s whose debits and credits balance.
/// </summary>
/// <remarks>
/// Invariant (enforced at construction): sum of all <see cref="JournalEntryLine.Debit"/> values
/// must equal sum of all <see cref="JournalEntryLine.Credit"/> values across all
/// <see cref="Lines"/>. Imbalanced entries are rejected with <see cref="ArgumentException"/>.
/// Use the constructor <c>JournalEntry(id, entryDate, memo, lines, createdAtUtc, sourceReference)</c>
/// to create instances.
/// </remarks>
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

    // ---------- W#60 P4 PR 3 — Stage 02 §3.3 extensions ----------

    /// <summary>
    /// FK to the <see cref="ChartOfAccounts"/> this entry posts against.
    /// Optional in PR 3; becomes mandatory once <see cref="ChartOfAccounts"/>
    /// registration enforces every <see cref="GLAccount"/> belongs to a chart.
    /// </summary>
    public ChartOfAccountsId? ChartId { get; init; }

    /// <summary>
    /// Wall-clock instant the entry transitioned to
    /// <see cref="JournalEntryStatus.Posted"/>. Null while
    /// <see cref="Status"/> is <see cref="JournalEntryStatus.Draft"/>.
    /// </summary>
    public Instant? PostedAtUtc { get; init; }

    /// <summary>
    /// Lifecycle state. Defaults to <see cref="JournalEntryStatus.Draft"/>
    /// for back-compat with pre-PR-3 call sites that did not specify
    /// a status.
    /// </summary>
    public JournalEntryStatus Status { get; init; } = JournalEntryStatus.Draft;

    /// <summary>
    /// Classification of the originating event (Invoice / Bill / Payment /
    /// etc.). Defaults to <see cref="JournalEntrySource.Manual"/> when
    /// unspecified.
    /// </summary>
    public JournalEntrySource SourceKind { get; init; } = JournalEntrySource.Manual;

    /// <summary>
    /// Backward FK on a reversing entry — points to the entry being
    /// reversed. Non-null only when <see cref="SourceKind"/> is
    /// <see cref="JournalEntrySource.Reversal"/>.
    /// </summary>
    public JournalEntryId? ReversalOf { get; init; }

    /// <summary>
    /// Forward FK on a reversed entry — points to the reversing entry
    /// that superseded this one. Non-null only when <see cref="Status"/>
    /// is <see cref="JournalEntryStatus.Reversed"/>.
    /// </summary>
    public JournalEntryId? ReversedBy { get; init; }

    /// <summary>
    /// FK to the fiscal period this entry posts within. Nullable in PR 3
    /// (the periods entity ships in <c>blocks-financial-periods</c> in a
    /// follow-on hand-off).
    /// </summary>
    public FiscalPeriodId? PeriodId { get; init; }

    /// <summary>
    /// Optional external system reference (e.g. ERPNext import id) so the
    /// migration importer can record provenance.
    /// </summary>
    public string? ExternalRef { get; init; }

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

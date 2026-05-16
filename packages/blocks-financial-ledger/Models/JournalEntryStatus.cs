namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Lifecycle state of a <see cref="JournalEntry"/> per Stage 02
/// <c>blocks-financial-schema-design.md</c> §3.3.
/// </summary>
public enum JournalEntryStatus
{
    /// <summary>
    /// In-progress edit; not yet committed to the ledger. Draft entries
    /// can be modified or discarded freely.
    /// </summary>
    Draft,

    /// <summary>
    /// Committed to the ledger. The entry is immutable once posted; any
    /// subsequent correction posts a separate reversing entry whose
    /// <see cref="JournalEntry.ReversalOf"/> points to this entry.
    /// </summary>
    Posted,

    /// <summary>
    /// A previously-posted entry that has been reversed by a later
    /// posting. Pointed at by another entry's
    /// <see cref="JournalEntry.ReversalOf"/>.
    /// </summary>
    Reversed,
}

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Classification of the originating event that produced a
/// <see cref="JournalEntry"/> per Stage 02
/// <c>blocks-financial-schema-design.md</c> §3.3. Drives audit reporting
/// and reverse-flow analysis (e.g. "all rekey-related entries posted
/// in the last 30 days").
/// </summary>
public enum JournalEntrySource
{
    /// <summary>Hand-keyed via the GL admin UI.</summary>
    Manual,

    /// <summary>Auto-posted from <c>blocks-financial-ar</c> Invoice issuance.</summary>
    Invoice,

    /// <summary>Auto-posted from <c>blocks-financial-ap</c> Bill creation.</summary>
    Bill,

    /// <summary>Auto-posted from <c>blocks-financial-payments</c> outgoing payment.</summary>
    Payment,

    /// <summary>Auto-posted from <c>blocks-financial-payments</c> incoming receipt.</summary>
    Receipt,

    /// <summary>Periodic auto-post from <see cref="DepreciationSchedule"/>.</summary>
    Depreciation,

    /// <summary>Adjusting entry (period-close accruals, deferrals).</summary>
    Adjusting,

    /// <summary>Period-close entry (e.g. close revenue/expense to retained earnings).</summary>
    Closing,

    /// <summary>
    /// Reverses a prior posted entry; the reversing entry's
    /// <see cref="JournalEntry.ReversalOf"/> points to the original.
    /// </summary>
    Reversal,

    /// <summary>One-way import from an external system (ERPNext, QuickBooks, etc.).</summary>
    Migration,
}

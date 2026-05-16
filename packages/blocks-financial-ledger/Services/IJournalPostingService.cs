using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Atomic posting service for journal entries per Stage 02
/// <c>blocks-financial-schema-design.md</c> §6.1. Implements the six-phase
/// posting algorithm: preconditions → balance → account-validity →
/// period-gating → atomic commit → result.
/// </summary>
public interface IJournalPostingService
{
    /// <summary>
    /// Validate <paramref name="entry"/> against the six-phase algorithm
    /// and, on success, persist it via <see cref="IJournalStore"/> with
    /// <see cref="JournalEntry.Status"/> = <see cref="JournalEntryStatus.Posted"/>
    /// and <see cref="JournalEntry.PostedAtUtc"/> populated. Returns a
    /// <see cref="PostResult"/> describing success or the structured
    /// failure reason — does not throw on validation failure.
    /// </summary>
    Task<PostResult> PostAsync(
        JournalEntry entry,
        CancellationToken cancellationToken = default);
}

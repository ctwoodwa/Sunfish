using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Atomic write boundary for journal entries. Implementations wrap the
/// underlying persistence (SQLite in production) and surface a
/// commit-or-rollback semantic the
/// <see cref="JournalPostingService"/> can drive without knowing the
/// storage details.
/// </summary>
public interface IJournalStore
{
    /// <summary>
    /// Persist <paramref name="entry"/> + its lines as a single atomic
    /// unit. Throws on any failure — implementations MUST roll back any
    /// partial writes before propagating.
    /// </summary>
    Task SaveAtomicAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshot of persisted entries — for test assertions about
    /// rollback / partial-write behaviour. Implementations may return
    /// a defensive copy.
    /// </summary>
    IReadOnlyList<JournalEntry> Snapshot();
}

/// <summary>
/// In-memory <see cref="IJournalStore"/>. Saves to a backing list;
/// supports an injected failure-trigger predicate so tests can induce
/// commit-time exceptions to exercise the rollback path.
/// </summary>
public sealed class InMemoryJournalStore : IJournalStore
{
    private readonly List<JournalEntry> _entries = new();
    private readonly object _gate = new();

    /// <summary>
    /// If set, invoked before each save. Returning <c>true</c> raises
    /// an <see cref="InvalidOperationException"/> simulating a
    /// commit-time storage failure.
    /// </summary>
    public Func<JournalEntry, bool>? FailIf { get; set; }

    /// <inheritdoc />
    public Task SaveAtomicAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        if (FailIf is not null && FailIf(entry))
        {
            // Simulate a mid-commit storage failure. NO partial state
            // mutation — the list is unchanged.
            throw new InvalidOperationException("InMemoryJournalStore: induced failure for rollback test.");
        }

        lock (_gate)
        {
            _entries.Add(entry);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }
}

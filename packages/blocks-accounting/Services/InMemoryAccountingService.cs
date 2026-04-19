using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Accounting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IAccountingService"/>.
/// Suitable for testing, prototyping, and kitchen-sink demos.
/// Not intended for production persistence — use a database-backed implementation for that.
/// </summary>
public sealed class InMemoryAccountingService : IAccountingService
{
    private readonly ConcurrentDictionary<GLAccountId, GLAccount> _accounts = new();

    // Index from account code → id for duplicate-code detection.
    private readonly ConcurrentDictionary<string, GLAccountId> _codeIndex = new();

    private readonly ConcurrentDictionary<JournalEntryId, JournalEntry> _entries = new();

    private readonly ConcurrentDictionary<DepreciationScheduleId, DepreciationSchedule> _depreciationSchedules = new();

    // Semaphore serialising account creation to make the code+index write atomic.
    private readonly SemaphoreSlim _accountWriteLock = new(1, 1);

    // ---------------------------------------------------------------------------
    // GL Accounts
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async ValueTask<GLAccount> CreateAccountAsync(CreateGLAccountRequest request, CancellationToken ct = default)
    {
        await _accountWriteLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_codeIndex.ContainsKey(request.Code))
                throw new InvalidOperationException(
                    $"A GL account with code '{request.Code}' already exists.");

            var account = new GLAccount(
                Id: GLAccountId.NewId(),
                Code: request.Code,
                Name: request.Name,
                Type: request.Type,
                ParentAccountId: request.ParentAccountId);

            _accounts[account.Id] = account;
            _codeIndex[account.Code] = account.Id;
            return account;
        }
        finally
        {
            _accountWriteLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<GLAccount?> GetAccountAsync(GLAccountId id, CancellationToken ct = default)
    {
        _accounts.TryGetValue(id, out var account);
        return ValueTask.FromResult(account);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GLAccount> ListAccountsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var account in _accounts.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return account;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------------
    // Journal Entries
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public ValueTask<JournalEntry> PostEntryAsync(PostJournalEntryRequest request, CancellationToken ct = default)
    {
        // Validate all referenced account IDs exist before constructing the entry.
        foreach (var line in request.Lines)
        {
            if (!_accounts.ContainsKey(line.AccountId))
                throw new KeyNotFoundException(
                    $"GL account '{line.AccountId}' referenced in a journal entry line does not exist.");
        }

        // JournalEntry constructor enforces balance + line invariants; let it throw naturally.
        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: request.EntryDate,
            memo: request.Memo,
            lines: request.Lines,
            createdAtUtc: Instant.Now,
            sourceReference: request.SourceReference);

        _entries[entry.Id] = entry;
        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc />
    public ValueTask<JournalEntry?> GetEntryAsync(JournalEntryId id, CancellationToken ct = default)
    {
        _entries.TryGetValue(id, out var entry);
        return ValueTask.FromResult(entry);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<JournalEntry> ListEntriesAsync(
        ListEntriesQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var entry in _entries.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.FromDate is not null && entry.EntryDate < query.FromDate.Value)
                continue;
            if (query.ToDate is not null && entry.EntryDate > query.ToDate.Value)
                continue;

            yield return entry;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------------
    // Depreciation Schedules
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public ValueTask<DepreciationSchedule> RegisterScheduleAsync(
        RegisterDepreciationRequest request,
        CancellationToken ct = default)
    {
        var schedule = new DepreciationSchedule(
            Id: DepreciationScheduleId.NewId(),
            AssetId: request.AssetId,
            StartDate: request.StartDate,
            OriginalCost: request.OriginalCost,
            SalvageValue: request.SalvageValue,
            UsefulLifeMonths: request.UsefulLifeMonths,
            Method: request.Method);

        _depreciationSchedules[schedule.Id] = schedule;
        return ValueTask.FromResult(schedule);
    }
}

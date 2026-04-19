using Sunfish.Blocks.Accounting.Models;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Core service contract for the accounting domain.
/// Provides GL account management, manual journal entry posting, and depreciation schedule registration.
/// </summary>
/// <remarks>
/// This is the first-pass (G17, Option B) service contract. The following are explicitly deferred:
/// <list type="bullet">
///   <item>Automatic JE generation from payment events (requires event bus — follow-up).</item>
///   <item>Depreciation schedule computation (passive shape only this pass — follow-up).</item>
///   <item>Xero export (QuickBooks IIF only in this pass — follow-up).</item>
///   <item>Ledger UI Blazor block (export-only library this pass — follow-up).</item>
/// </list>
/// </remarks>
public interface IAccountingService
{
    // -------------------------------------------------------------------------
    // GL Accounts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates and persists a new GL account.
    /// </summary>
    /// <param name="request">Account configuration including code, name, and type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="GLAccount"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an account with the same <see cref="CreateGLAccountRequest.Code"/> already exists.
    /// </exception>
    ValueTask<GLAccount> CreateAccountAsync(CreateGLAccountRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single GL account by its identifier.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="GLAccount"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<GLAccount?> GetAccountAsync(GLAccountId id, CancellationToken ct = default);

    /// <summary>
    /// Lists all GL accounts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of all <see cref="GLAccount"/> records.</returns>
    IAsyncEnumerable<GLAccount> ListAccountsAsync(CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // Journal Entries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates and posts a new journal entry.
    /// </summary>
    /// <param name="request">
    /// Entry details including date, memo, and balanced debit/credit lines.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The posted <see cref="JournalEntry"/> as persisted.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the entry is imbalanced (total debits ≠ total credits), or when a line
    /// violates single-side invariants (both non-zero, both zero, or negative amounts).
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when any <see cref="JournalEntryLine.AccountId"/> references a GL account
    /// that does not exist.
    /// </exception>
    ValueTask<JournalEntry> PostEntryAsync(PostJournalEntryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single journal entry by its identifier.
    /// </summary>
    /// <param name="id">Journal entry identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="JournalEntry"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<JournalEntry?> GetEntryAsync(JournalEntryId id, CancellationToken ct = default);

    /// <summary>
    /// Lists journal entries matching the supplied filter query.
    /// Returns all entries when the query contains no filter constraints.
    /// </summary>
    /// <param name="query">Optional date-range filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of matching <see cref="JournalEntry"/> records.</returns>
    IAsyncEnumerable<JournalEntry> ListEntriesAsync(ListEntriesQuery query, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // Depreciation Schedules (passive shape only — no computation this pass)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a new depreciation schedule for a fixed asset.
    /// </summary>
    /// <remarks>
    /// Schedule-line computation is deferred. This method stores the configuration
    /// for future use. See the TODO in <see cref="DepreciationSchedule"/> for the follow-up.
    /// </remarks>
    /// <param name="request">Asset reference, cost basis, useful life, and depreciation method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The registered <see cref="DepreciationSchedule"/>.</returns>
    ValueTask<DepreciationSchedule> RegisterScheduleAsync(RegisterDepreciationRequest request, CancellationToken ct = default);
}

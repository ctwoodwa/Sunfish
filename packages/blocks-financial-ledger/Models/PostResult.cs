namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Outcome of <see cref="Services.IJournalPostingService.PostAsync"/>.
/// On success, <see cref="Entry"/> carries the promoted entry
/// (<see cref="JournalEntry.Status"/> = <see cref="JournalEntryStatus.Posted"/>
/// and <see cref="JournalEntry.PostedAtUtc"/> populated). On failure,
/// <see cref="Entry"/> is null and <see cref="Error"/> identifies the
/// reason — <see cref="Detail"/> carries the specific id / value when
/// useful.
/// </summary>
/// <param name="Entry">The posted entry on success; null on failure.</param>
/// <param name="Error">
/// The error code; <see cref="PostError.None"/> on success.
/// </param>
/// <param name="Detail">
/// Optional diagnostic detail (e.g. account id for
/// <see cref="PostError.UnknownAccount"/>, <c>"debits=X, credits=Y"</c>
/// for <see cref="PostError.Imbalanced"/>).
/// </param>
public readonly record struct PostResult(
    JournalEntry? Entry,
    PostError Error,
    string? Detail)
{
    /// <summary>Quick predicate — <c>true</c> when <see cref="Error"/> is <see cref="PostError.None"/>.</summary>
    public bool IsSuccess => Error == PostError.None;
}

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Optional filter criteria for <see cref="IAccountingService.ListEntriesAsync"/>.
/// All fields are optional; an empty query returns all journal entries.
/// </summary>
/// <param name="FromDate">
/// If set, only entries with <c>EntryDate &gt;= FromDate</c> are returned.
/// </param>
/// <param name="ToDate">
/// If set, only entries with <c>EntryDate &lt;= ToDate</c> are returned.
/// </param>
public sealed record ListEntriesQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

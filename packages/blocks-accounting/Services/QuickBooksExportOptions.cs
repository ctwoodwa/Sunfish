namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Options controlling QuickBooks IIF export behaviour.
/// </summary>
/// <param name="AccountName">
/// Default account name to use in the IIF <c>ACCNT</c> field when the GL account code
/// is not available. Defaults to <c>"Unspecified"</c>.
/// </param>
/// <param name="IncludeSourceReference">
/// When <see langword="true"/> (default), writes the <see cref="JournalEntry.SourceReference"/>
/// value into the IIF <c>MEMO</c> field of each SPL line, prefixed with <c>"[src]"</c>.
/// When <see langword="false"/>, the line-level MEMO is left blank.
/// </param>
public sealed record QuickBooksExportOptions(
    string AccountName = "Unspecified",
    bool IncludeSourceReference = true);

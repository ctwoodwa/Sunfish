using Sunfish.Blocks.Accounting.Models;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Formats a batch of journal entries as a QuickBooks IIF (Intuit Interchange Format) string
/// suitable for import via QuickBooks Desktop's <em>File → Utilities → Import → IIF Files</em>
/// dialog.
/// </summary>
/// <remarks>
/// <b>Export format: IIF (Intuit Interchange Format)</b>
/// <para>
/// IIF is QuickBooks' classic flat-file import format. It was chosen over QBO REST-API JSON for
/// three reasons:
/// <list type="number">
///   <item>
///     IIF is stable, well-documented, and SDK-free — no OAuth connection or heavyweight
///     QuickBooks SDK dependency is required.
///   </item>
///   <item>
///     Consumer-generable: any Sunfish host can call <see cref="Export"/> and write the result
///     to a file or stream without network access.
///   </item>
///   <item>
///     Predictable column positions make the output easy to verify in unit tests and easy to
///     inspect in a plain-text editor.
///   </item>
/// </list>
/// </para>
/// <para>
/// IIF structure used:
/// <code>
/// !TRNS  DATE  ACCNT  MEMO  AMOUNT  CLASS
/// !SPL   DATE  ACCNT  MEMO  AMOUNT
/// !ENDTRNS
/// TRNS   {date}  {account-code}  {memo}  {first-debit}
/// SPL    {date}  {account-code}  {line-memo}  {-amount}
/// ...
/// ENDTRNS
/// </code>
/// Amounts follow the IIF convention: positive = debit (money in), negative = credit (money out)
/// relative to the TRNS account.
/// </para>
/// <para>
/// <b>Empty-batch behaviour:</b> when <paramref name="entries"/> is empty, only the header block
/// (<c>!TRNS</c> / <c>!SPL</c> / <c>!ENDTRNS</c>) is emitted. This is intentional — a valid IIF
/// file with no data rows is still a valid IIF file.
/// </para>
/// </remarks>
public interface IQuickBooksJournalEntryExporter
{
    /// <summary>
    /// Converts the supplied journal entries to IIF format.
    /// </summary>
    /// <param name="entries">
    /// Entries to export. An empty sequence produces a header-only IIF file.
    /// </param>
    /// <param name="options">Export behaviour options (account name fallback, source reference inclusion).</param>
    /// <returns>
    /// A UTF-8 IIF string terminated by a trailing newline.
    /// Fields within each line are separated by tab characters (<c>'\t'</c>).
    /// </returns>
    string Export(IEnumerable<JournalEntry> entries, QuickBooksExportOptions options);
}

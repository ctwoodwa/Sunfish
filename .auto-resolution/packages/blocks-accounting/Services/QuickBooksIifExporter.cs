using System.Text;
using Sunfish.Blocks.Accounting.Models;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Produces QuickBooks IIF (Intuit Interchange Format) output from a batch of
/// <see cref="JournalEntry"/> records.
/// </summary>
/// <remarks>
/// IIF column layout used:
/// <list type="table">
///   <listheader><term>Column</term><description>Meaning</description></listheader>
///   <item><term>TYPE</term><description>TRNS (header line) or SPL (split/detail line)</description></item>
///   <item><term>DATE</term><description>MM/DD/YYYY — QuickBooks' required locale-neutral date format</description></item>
///   <item><term>ACCNT</term><description>GL account code (matches the account code in QuickBooks)</description></item>
///   <item><term>MEMO</term><description>Transaction memo (TRNS) or optional line note (SPL)</description></item>
///   <item><term>AMOUNT</term><description>
///     Positive for debits on the TRNS line; negative for credits on SPL lines.
///     Each JE is split as: one TRNS row for the first line, one SPL row per remaining line.
///   </description></item>
/// </list>
/// <para>
/// <b>Amount sign convention:</b> IIF uses a single-sided amount column with the sign encoding
/// the direction. On the TRNS row the amount is positive (debit) or negative (credit) from the
/// perspective of the primary account. SPL lines use the opposite sign. This exporter writes
/// each <see cref="JournalEntryLine"/> faithfully: debit lines emit the positive debit amount;
/// credit lines emit the negative credit amount.
/// </para>
/// </remarks>
public sealed class QuickBooksIifExporter : IQuickBooksJournalEntryExporter
{
    // IIF header labels (tab-separated).
    private const string TrnsHeader = "!TRNS\tDATE\tACCNT\tMEMO\tAMOUNT";
    private const string SplHeader  = "!SPL\tDATE\tACCNT\tMEMO\tAMOUNT";
    private const string EndTrns    = "!ENDTRNS";

    /// <inheritdoc />
    public string Export(IEnumerable<JournalEntry> entries, QuickBooksExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);

        var sb = new StringBuilder();

        // Header block — always present, even for an empty batch.
        sb.AppendLine(TrnsHeader);
        sb.AppendLine(SplHeader);
        sb.AppendLine(EndTrns);

        foreach (var entry in entries)
        {
            var dateStr = entry.EntryDate.ToString("MM/dd/yyyy");
            var lines   = entry.Lines;

            if (lines.Count == 0) continue; // guard — JournalEntry constructor prevents this

            // First line → TRNS row (primary account / header of the transaction)
            var firstLine = lines[0];
            sb.Append("TRNS\t");
            sb.Append(dateStr);
            sb.Append('\t');
            sb.Append(EscapeField(firstLine.AccountId.Value));
            sb.Append('\t');
            sb.Append(EscapeField(entry.Memo));
            sb.Append('\t');
            sb.AppendLine(FormatAmount(firstLine));

            // Remaining lines → SPL rows
            for (var i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                var lineMemo = BuildSplMemo(entry, line, options);

                sb.Append("SPL\t");
                sb.Append(dateStr);
                sb.Append('\t');
                sb.Append(EscapeField(line.AccountId.Value));
                sb.Append('\t');
                sb.Append(EscapeField(lineMemo));
                sb.Append('\t');
                sb.AppendLine(FormatAmount(line));
            }

            sb.AppendLine("ENDTRNS");
        }

        return sb.ToString();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// IIF amount sign: debit lines are positive; credit lines are negative.
    /// This follows the standard IIF convention for journal entries (TRNS/SPL).
    /// </summary>
    private static string FormatAmount(JournalEntryLine line)
    {
        var amount = line.Debit != 0m ? line.Debit : -line.Credit;
        return amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildSplMemo(JournalEntry entry, JournalEntryLine line, QuickBooksExportOptions options)
    {
        if (options.IncludeSourceReference && entry.SourceReference is not null)
            return $"[src] {entry.SourceReference}";
        return line.Notes ?? string.Empty;
    }

    /// <summary>
    /// Removes tab and newline characters from a field value to prevent IIF row corruption.
    /// IIF is a tab-delimited format with no quoting; embedded tabs/newlines break parsing.
    /// </summary>
    private static string EscapeField(string value)
        => value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}

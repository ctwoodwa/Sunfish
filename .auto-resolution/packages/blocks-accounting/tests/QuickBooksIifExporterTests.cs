using Sunfish.Blocks.Accounting.Models;
using Sunfish.Blocks.Accounting.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Accounting.Tests;

public class QuickBooksIifExporterTests
{
    private static readonly QuickBooksIifExporter Exporter = new();
    private static readonly QuickBooksExportOptions DefaultOptions = new();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static JournalEntry MakeEntry(
        DateOnly date,
        string memo,
        IReadOnlyList<JournalEntryLine> lines,
        string? sourceRef = null)
        => new(
            id: JournalEntryId.NewId(),
            entryDate: date,
            memo: memo,
            lines: lines,
            createdAtUtc: Instant.Now,
            sourceReference: sourceRef);

    private static JournalEntry TwoLineEntry(
        DateOnly date,
        GLAccountId debitId,
        GLAccountId creditId,
        decimal amount,
        string memo = "Test",
        string? sourceRef = null)
    {
        var lines = new List<JournalEntryLine>
        {
            new(debitId, debit: amount, credit: 0m),
            new(creditId, debit: 0m, credit: amount),
        };
        return MakeEntry(date, memo, lines, sourceRef);
    }

    // =========================================================================
    // Empty batch
    // =========================================================================

    [Fact]
    public void Export_EmptyBatch_EmitsOnlyHeaderBlock()
    {
        var output = Exporter.Export([], DefaultOptions);

        // Must contain all three header lines
        Assert.Contains("!TRNS\tDATE\tACCNT\tMEMO\tAMOUNT", output);
        Assert.Contains("!SPL\tDATE\tACCNT\tMEMO\tAMOUNT", output);
        Assert.Contains("!ENDTRNS", output);

        // Must not contain any TRNS data rows (only the header row starting with '!')
        var dataLines = output.Split('\n')
            .Where(l => l.StartsWith("TRNS\t") || l.StartsWith("SPL\t") || l.StartsWith("ENDTRNS"))
            .ToList();
        Assert.Empty(dataLines);
    }

    // =========================================================================
    // Single balanced entry
    // =========================================================================

    [Fact]
    public void Export_SingleEntry_CorrectIifRoundTrip()
    {
        var debitId  = new GLAccountId("1000");
        var creditId = new GLAccountId("4000");
        var date     = new DateOnly(2025, 6, 15);

        var entry = TwoLineEntry(date, debitId, creditId, 750m, memo: "Rent received");
        var output = Exporter.Export([entry], DefaultOptions);

        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        // Find the TRNS data row
        var trnsRow = lines.FirstOrDefault(l => l.StartsWith("TRNS\t"))
            ?? throw new Exception("No TRNS data row found");
        var trnsCols = trnsRow.Split('\t');

        Assert.Equal("TRNS", trnsCols[0]);
        Assert.Equal("06/15/2025", trnsCols[1]);   // MM/dd/yyyy
        Assert.Equal("1000", trnsCols[2]);          // debit account code
        Assert.Equal("Rent received", trnsCols[3]); // memo
        Assert.Equal("750.00", trnsCols[4]);        // positive = debit

        // Find the SPL data row
        var splRow = lines.FirstOrDefault(l => l.StartsWith("SPL\t"))
            ?? throw new Exception("No SPL data row found");
        var splCols = splRow.Split('\t');

        Assert.Equal("SPL", splCols[0]);
        Assert.Equal("06/15/2025", splCols[1]);
        Assert.Equal("4000", splCols[2]);   // credit account code
        Assert.Equal("-750.00", splCols[4]); // negative = credit

        // Must close with ENDTRNS
        Assert.Contains(lines, l => l == "ENDTRNS");
    }

    // =========================================================================
    // Multi-line entry — all lines preserved in order
    // =========================================================================

    [Fact]
    public void Export_MultiLineEntry_PreservesAllLinesInOrder()
    {
        // 3-line split entry: one debit, two credits
        var cashId        = new GLAccountId("1000");
        var revenueId     = new GLAccountId("4000");
        var taxPayableId  = new GLAccountId("2100");
        var date          = new DateOnly(2025, 7, 1);

        var entryLines = new List<JournalEntryLine>
        {
            new(cashId,       debit: 1100m, credit: 0m),
            new(revenueId,    debit: 0m, credit: 1000m),
            new(taxPayableId, debit: 0m, credit: 100m),
        };
        var entry  = MakeEntry(date, "Split rent with tax", entryLines);
        var output = Exporter.Export([entry], DefaultOptions);

        var dataLines = output.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("TRNS\t") || l.StartsWith("SPL\t"))
            .ToList();

        Assert.Equal(3, dataLines.Count);  // 1 TRNS + 2 SPL

        var trnsCols = dataLines[0].Split('\t');
        Assert.Equal("TRNS",   trnsCols[0]);
        Assert.Equal("1000",   trnsCols[2]);
        Assert.Equal("1100.00", trnsCols[4]);

        var spl1Cols = dataLines[1].Split('\t');
        Assert.Equal("SPL",    spl1Cols[0]);
        Assert.Equal("4000",   spl1Cols[2]);
        Assert.Equal("-1000.00", spl1Cols[4]);

        var spl2Cols = dataLines[2].Split('\t');
        Assert.Equal("SPL",    spl2Cols[0]);
        Assert.Equal("2100",   spl2Cols[2]);
        Assert.Equal("-100.00", spl2Cols[4]);
    }

    // =========================================================================
    // Memo and SourceReference in IIF columns
    // =========================================================================

    [Fact]
    public void Export_SourceReferenceIncluded_AppearsInSplMemoColumn()
    {
        var debitId  = new GLAccountId("1000");
        var creditId = new GLAccountId("4000");

        var entry = TwoLineEntry(
            date: new DateOnly(2025, 8, 1),
            debitId: debitId,
            creditId: creditId,
            amount: 300m,
            memo: "Payment received",
            sourceRef: "rent-payment:INV-007");

        var output = Exporter.Export([entry], new QuickBooksExportOptions(IncludeSourceReference: true));

        var splRow = output.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .First(l => l.StartsWith("SPL\t"));

        var splCols = splRow.Split('\t');
        // MEMO column (index 3) on SPL row should contain the source reference
        Assert.Contains("rent-payment:INV-007", splCols[3]);
    }

    [Fact]
    public void Export_SourceReferenceExcluded_SplMemoIsBlankOrLineNotes()
    {
        var debitId  = new GLAccountId("1000");
        var creditId = new GLAccountId("4000");

        var entry = TwoLineEntry(
            date: new DateOnly(2025, 8, 1),
            debitId: debitId,
            creditId: creditId,
            amount: 300m,
            memo: "Payment received",
            sourceRef: "rent-payment:INV-007");

        var output = Exporter.Export([entry], new QuickBooksExportOptions(IncludeSourceReference: false));

        var splRow = output.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .First(l => l.StartsWith("SPL\t"));

        var splCols = splRow.Split('\t');
        // IncludeSourceReference=false: source ref must NOT appear in the SPL memo column
        Assert.DoesNotContain("rent-payment:INV-007", splCols[3]);
    }
}

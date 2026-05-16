using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 3 — coverage for the <see cref="JournalEntry"/> and
/// <see cref="JournalEntryLine"/> Stage 02 §3.3–§3.4 schema
/// extensions. Verifies init-only property round-trips, status
/// transitions via <c>with</c> expressions, ReversalOf / ReversedBy
/// linkage, dimensional-tag presence on lines, and the existing
/// balance-check constructor invariant remains intact.
/// </summary>
public sealed class JournalEntryExtensionsTests
{
    private static readonly GLAccountId AccountA = GLAccountId.NewId();
    private static readonly GLAccountId AccountB = GLAccountId.NewId();

    [Fact]
    public void JournalEntry_Defaults_StatusDraft_SourceManual()
    {
        var je = BuildBalancedEntry();
        Assert.Equal(JournalEntryStatus.Draft, je.Status);
        Assert.Equal(JournalEntrySource.Manual, je.SourceKind);
        Assert.Null(je.ChartId);
        Assert.Null(je.PostedAtUtc);
        Assert.Null(je.ReversalOf);
        Assert.Null(je.ReversedBy);
        Assert.Null(je.PeriodId);
        Assert.Null(je.ExternalRef);
    }

    [Fact]
    public void JournalEntry_With_StatusTransition_DraftToPosted()
    {
        var draft = BuildBalancedEntry();
        var posted = draft with
        {
            Status = JournalEntryStatus.Posted,
            PostedAtUtc = Instant.Now,
        };

        Assert.Equal(JournalEntryStatus.Draft, draft.Status);
        Assert.Equal(JournalEntryStatus.Posted, posted.Status);
        Assert.NotNull(posted.PostedAtUtc);
        // `with` returns a new instance and leaves the original immutable.
        Assert.NotSame(draft, posted);
    }

    [Fact]
    public void JournalEntry_ReversalLinkage_RoundTrips()
    {
        var original = BuildBalancedEntry() with
        {
            Status = JournalEntryStatus.Posted,
            PostedAtUtc = Instant.Now,
        };
        var reversing = BuildBalancedEntry(
            debitAccount: AccountB, creditAccount: AccountA) with
        {
            Status = JournalEntryStatus.Posted,
            PostedAtUtc = Instant.Now,
            SourceKind = JournalEntrySource.Reversal,
            ReversalOf = original.Id,
        };
        var supersededOriginal = original with
        {
            Status = JournalEntryStatus.Reversed,
            ReversedBy = reversing.Id,
        };

        Assert.Equal(JournalEntrySource.Reversal, reversing.SourceKind);
        Assert.Equal(original.Id, reversing.ReversalOf);
        Assert.Equal(JournalEntryStatus.Reversed, supersededOriginal.Status);
        Assert.Equal(reversing.Id, supersededOriginal.ReversedBy);
    }

    [Fact]
    public void JournalEntry_ExternalRef_AndPeriodId_RoundTrip()
    {
        var period = FiscalPeriodId.NewId();
        var je = BuildBalancedEntry() with
        {
            PeriodId = period,
            ExternalRef = "erpnext:JE-12345",
        };
        Assert.Equal(period, je.PeriodId);
        Assert.Equal("erpnext:JE-12345", je.ExternalRef);
    }

    [Fact]
    public void JournalEntry_PreservesBalanceCheck_OnImbalancedLines()
    {
        // PR 3 must NOT loosen the constructor's balance invariant.
        var lines = new[]
        {
            new JournalEntryLine(AccountA, debit: 100m, credit: 0m),
            new JournalEntryLine(AccountB, debit: 0m, credit: 50m), // <- imbalanced
        };
        var ex = Assert.Throws<ArgumentException>(() => new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: "imbalanced",
            lines: lines,
            createdAtUtc: Instant.Now));
        Assert.Contains("imbalanced", ex.Message);
    }

    [Fact]
    public void JournalEntryLine_Defaults_AllDimensionalTagsNull()
    {
        var line = new JournalEntryLine(AccountA, debit: 100m, credit: 0m);
        Assert.Null(line.PropertyId);
        Assert.Null(line.ClassId);
        Assert.Null(line.TaxCodeId);
    }

    [Fact]
    public void JournalEntryLine_With_DimensionalTags_RoundTrip()
    {
        var prop = PropertyId.NewId();
        var cls = ClassificationId.NewId();
        var tax = TaxCodeId.NewId();
        var line = new JournalEntryLine(AccountA, debit: 100m, credit: 0m, notes: "rent")
        {
            PropertyId = prop,
            ClassId = cls,
            TaxCodeId = tax,
        };
        Assert.Equal(prop, line.PropertyId);
        Assert.Equal(cls, line.ClassId);
        Assert.Equal(tax, line.TaxCodeId);
    }

    [Fact]
    public void JournalEntryLine_With_DimensionalTags_DoesNotMutateOriginal()
    {
        var line = new JournalEntryLine(AccountA, debit: 100m, credit: 0m);
        var tagged = line with { PropertyId = PropertyId.NewId() };
        Assert.Null(line.PropertyId);
        Assert.NotNull(tagged.PropertyId);
    }

    // ----- helpers ---------------------------------------------------

    private static JournalEntry BuildBalancedEntry(
        GLAccountId? debitAccount = null,
        GLAccountId? creditAccount = null,
        decimal amount = 100m)
    {
        var debitAcc  = debitAccount  ?? AccountA;
        var creditAcc = creditAccount ?? AccountB;
        return new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: "test",
            lines: new[]
            {
                new JournalEntryLine(debitAcc,  debit: amount, credit: 0m),
                new JournalEntryLine(creditAcc, debit: 0m, credit: amount),
            },
            createdAtUtc: Instant.Now);
    }
}

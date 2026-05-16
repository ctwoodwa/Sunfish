using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="FiscalYear"/> per Stage 02 §3.15.
/// </summary>
public sealed class FiscalYearTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public void CreateOpen_PopulatesStatusOpen_AndNullCloseFields()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(FiscalYearStatus.Open, fy.Status);
        Assert.Null(fy.ClosedAtUtc);
        Assert.Null(fy.ClosingJournalEntryId);
    }

    [Fact]
    public void CreateOpen_PopulatesCreatedAtUtc_WhenNotProvided()
    {
        var before = Instant.Now;
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var after = Instant.Now;

        Assert.True(fy.CreatedAtUtc.Value >= before.Value);
        Assert.True(fy.CreatedAtUtc.Value <= after.Value);
    }

    [Fact]
    public void Validate_RejectsStartAfterEnd()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            startDate: new DateOnly(2026, 6, 1),
            endDate:   new DateOnly(2026, 1, 1));
        var errors = fy.Validate();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("StartDate"));
    }

    [Fact]
    public void Validate_RejectsEmptyLabel()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var errors = fy.Validate();
        Assert.Contains(errors, e => e.Contains("Label"));
    }

    [Fact]
    public void Validate_RejectsClosedWithNullClosedAtUtc()
    {
        var fy = new FiscalYear(
            Id: FiscalYearId.NewId(), ChartId: Chart, Label: "2026",
            StartDate: new DateOnly(2026, 1, 1), EndDate: new DateOnly(2026, 12, 31),
            Status: FiscalYearStatus.Closed,
            ClosedAtUtc: null, ClosingJournalEntryId: null,
            CreatedAtUtc: Instant.Now);
        var errors = fy.Validate();
        Assert.Contains(errors, e => e.Contains("ClosedAtUtc"));
    }

    [Fact]
    public void Validate_RejectsOpenWithNonNullClosedAtUtc()
    {
        var fy = new FiscalYear(
            Id: FiscalYearId.NewId(), ChartId: Chart, Label: "2026",
            StartDate: new DateOnly(2026, 1, 1), EndDate: new DateOnly(2026, 12, 31),
            Status: FiscalYearStatus.Open,
            ClosedAtUtc: Instant.Now, ClosingJournalEntryId: null,
            CreatedAtUtc: Instant.Now);
        var errors = fy.Validate();
        Assert.Contains(errors, e => e.Contains("ClosedAtUtc"));
    }

    [Fact]
    public void Validate_AcceptsWellFormedOpenYear()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        Assert.Empty(fy.Validate());
    }
}

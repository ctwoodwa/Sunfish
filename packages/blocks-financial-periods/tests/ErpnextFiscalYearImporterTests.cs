using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for
/// <see cref="ErpnextFiscalYearImporter"/> idempotency + label
/// derivation + closed-FY-no-reopen invariant.
/// </summary>
public sealed class ErpnextFiscalYearImporterTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task Upsert_NewSource_InsertsFiscalYear()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "2026-04-15 10:00:00");

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.NotNull(outcome.Record);
        Assert.Equal(FiscalYearStatus.Open, outcome.Record.Status);
        Assert.Equal(Chart, outcome.Record.ChartId);
    }

    [Fact]
    public async Task Upsert_SameVersion_ReturnsSkipped()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "2026-04-15 10:00:00");
        await h.Sut.UpsertFromErpnextAsync(source, Chart);

        var again = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task Upsert_HigherVersion_ReturnsUpdated()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "2026-04-15 10:00:00");
        await h.Sut.UpsertFromErpnextAsync(source, Chart);

        var updated = await h.Sut.UpsertFromErpnextAsync(
            source with
            {
                Modified         = "2026-06-15 10:00:00",
                CompanyShortName = "AcerNew",
            },
            Chart);

        Assert.Equal(ImportAction.Updated, updated.Action);
        Assert.Contains("AcerNew", updated.Record.Label);
    }

    [Fact]
    public async Task Upsert_LowerVersion_ReturnsSkipped()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "2026-06-15 10:00:00");
        await h.Sut.UpsertFromErpnextAsync(source, Chart);

        var older = await h.Sut.UpsertFromErpnextAsync(
            source with { Modified = "2026-04-15 10:00:00" },
            Chart);

        Assert.Equal(ImportAction.Skipped, older.Action);
    }

    [Fact]
    public async Task Upsert_ClosedFy_HigherVersion_DoesNotReopen()
    {
        var h = new Harness();
        var source = NewSource("FY 2025", "2025-04-15 10:00:00");
        var first = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        // Force the local row to Closed (simulating an admin year-end
        // close that happened between exports).
        await h.Years.UpdateAsync(first.Record with
        {
            Status      = FiscalYearStatus.Closed,
            ClosedAtUtc = Instant.Now,
            Version     = first.Record.Version + 1,
        });

        // Re-export with bumped version.
        var refreshed = await h.Sut.UpsertFromErpnextAsync(
            source with
            {
                Modified         = "2026-04-15 10:00:00",
                CompanyShortName = "Acero",
            },
            Chart);

        Assert.Equal(ImportAction.Updated, refreshed.Action);
        // Status must stay Closed — importer does NOT reopen.
        Assert.Equal(FiscalYearStatus.Closed, refreshed.Record.Status);
        // ClosedAtUtc must be preserved by the `with` clone — guard
        // against a future regression that sets ClosedAtUtc = null in
        // the update path.
        Assert.NotNull(refreshed.Record.ClosedAtUtc);
    }

    [Fact]
    public async Task Upsert_MalformedModified_ThrowsFormatException()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "not-a-timestamp");

        await Assert.ThrowsAsync<FormatException>(
            () => h.Sut.UpsertFromErpnextAsync(source, Chart));
    }

    [Fact]
    public async Task Upsert_AcceptsFrappeMicrosecondsFormat()
    {
        var h = new Harness();
        var source = NewSource("FY 2026", "2026-04-15 10:00:00.123456");

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Inserted, outcome.Action);
    }

    [Fact]
    public async Task Upsert_PreservesExternalRef()
    {
        var h = new Harness();
        var source = NewSource("FY 2026 Acero", "2026-04-15 10:00:00");

        await h.Sut.UpsertFromErpnextAsync(source, Chart);

        // Re-upsert (Skipped path) should find the same row by
        // external-ref lookup.
        var again = await h.Sut.UpsertFromErpnextAsync(source, Chart);
        Assert.Equal(ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task Upsert_DerivesLabel_FromCompanyAndStartDate()
    {
        var h = new Harness();
        var source = NewSource(
            "FY 2026 Bosco",
            "2026-04-15 10:00:00",
            companyShortName: "Bosco");

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal("Bosco FY26", outcome.Record.Label);
    }

    [Fact]
    public async Task Upsert_NoCompany_StillDerivesLabel()
    {
        var h = new Harness();
        var source = NewSource("FY 2026 Unbranded", "2026-04-15 10:00:00", companyShortName: null);

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal("FY26", outcome.Record.Label);
    }

    // ----- helpers ---------------------------------------------------

    private static ErpnextFiscalYearSource NewSource(
        string name,
        string modified,
        string? companyShortName = "Acero",
        bool isShortYear = false)
        => new(
            Name:             name,
            Modified:         modified,
            YearStartDate:    new DateOnly(2026, 1, 1),
            YearEndDate:      new DateOnly(2026, 12, 31),
            CompanyShortName: companyShortName,
            IsShortYear:      isShortYear);

    private sealed class Harness
    {
        public InMemoryFiscalYearRepository Years { get; } = new();
        public ErpnextFiscalYearImporter Sut { get; }

        public Harness()
        {
            Sut = new ErpnextFiscalYearImporter(Years, TimeProvider.System);
        }
    }
}

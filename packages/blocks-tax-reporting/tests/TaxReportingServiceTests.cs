using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Blocks.TaxReporting.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Tests;

public sealed class TaxReportingServiceTests
{
    private static ITaxReportingService CreateService() => new InMemoryTaxReportingService();

    private static EntityId MakePropertyId(string local = "prop1")
        => EntityId.Parse($"property:test/{local}");

    // -----------------------------------------------------------------------
    // Schedule E generation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateScheduleE_ComputesNetIncomeOrLossCorrectly()
    {
        var svc = CreateService();
        var rows = new[]
        {
            new SchedulePropertyRow(MakePropertyId("p1"), "123 Main St",
                RentsReceived: 12000m,
                MortgageInterest: 4000m,
                Taxes: 1200m,
                Insurance: 600m,
                Repairs: 300m,
                Depreciation: 800m,
                OtherExpenses: 100m),
            new SchedulePropertyRow(MakePropertyId("p2"), "456 Oak Ave",
                RentsReceived: 18000m,
                MortgageInterest: 6000m,
                Taxes: 1800m,
                Insurance: 900m,
                Repairs: 500m,
                Depreciation: 1200m,
                OtherExpenses: 200m),
        };

        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024), rows));

        Assert.Equal(TaxReportStatus.Draft, report.Status);
        Assert.Equal(TaxReportKind.ScheduleE, report.Kind);

        var body = Assert.IsType<ScheduleEBody>(report.Body);
        Assert.Equal(30_000m, body.TotalRents);          // 12000 + 18000
        Assert.Equal(17_600m, body.TotalExpenses);       // (4000+1200+600+300+800+100) + (6000+1800+900+500+1200+200)
        Assert.Equal(12_400m, body.NetIncomeOrLoss);     // 30000 - 17600
    }

    [Fact]
    public async Task GenerateScheduleE_SingleProperty_NetLoss()
    {
        var svc = CreateService();
        var rows = new[]
        {
            new SchedulePropertyRow(MakePropertyId("p1"), "1 Loss Lane",
                RentsReceived: 5000m,
                MortgageInterest: 6000m,
                Taxes: 500m,
                Insurance: 200m,
                Repairs: 100m,
                Depreciation: 400m,
                OtherExpenses: 50m),
        };

        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024), rows));
        var body = Assert.IsType<ScheduleEBody>(report.Body);

        Assert.Equal(-2250m, body.NetIncomeOrLoss); // 5000 - 7250
    }

    // -----------------------------------------------------------------------
    // 1099-NEC generation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Generate1099Nec_FiltersOutBelowThresholdRecipients()
    {
        var svc = CreateService();
        var recipients = new[]
        {
            new Nec1099Recipient("Alice Contractor", "XXX-XX-1234", "1 Main St", 1500m),
            new Nec1099Recipient("Bob Handyman",     "XXX-XX-5678", "2 Oak Ave",  500m), // below $600
            new Nec1099Recipient("Carol Plumber",    "XXX-XX-9999", "3 Elm Rd",  600m), // exactly at threshold
        };

        var report = await svc.Generate1099NecAsync(
            new Nec1099GenerationRequest(new TaxYear(2024), recipients));

        Assert.Equal(TaxReportStatus.Draft, report.Status);
        var body = Assert.IsType<Form1099NecBody>(report.Body);

        Assert.Equal(2, body.Recipients.Count);
        Assert.All(body.Recipients, r => Assert.True(r.MeetsThreshold));
        Assert.DoesNotContain(body.Recipients, r => r.RecipientName == "Bob Handyman");
    }

    // -----------------------------------------------------------------------
    // FinalizeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_OnDraft_MovesToFinalizedAndComputesSignatureValue()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 10000m, 3000m, 500m, 300m, 100m, 500m, 50m)]));

        Assert.Null(report.SignatureValue);

        var finalized = await svc.FinalizeAsync(report.Id);

        Assert.Equal(TaxReportStatus.Finalized, finalized.Status);
        Assert.NotNull(finalized.SignatureValue);
        Assert.Equal(64, finalized.SignatureValue!.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public async Task FinalizeAsync_ProducesStableHash_SameContentSameHash()
    {
        var svc = CreateService();

        var rows = new[]
        {
            new SchedulePropertyRow(MakePropertyId("p1"), "1 Main St",
                10000m, 3000m, 500m, 300m, 100m, 500m, 50m),
        };

        var report1 = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024), rows));
        var report2 = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024), rows));

        var fin1 = await svc.FinalizeAsync(report1.Id);
        var fin2 = await svc.FinalizeAsync(report2.Id);

        // Same body content → same hash regardless of different report IDs / timestamps.
        Assert.Equal(fin1.SignatureValue, fin2.SignatureValue);
    }

    [Fact]
    public async Task FinalizeAsync_ProducesDifferentHash_WhenContentDiffers()
    {
        var svc = CreateService();

        var report1 = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId("p1"), "1 Main", 10000m, 3000m, 500m, 300m, 100m, 500m, 50m)]));

        var report2 = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId("p1"), "1 Main", 99999m, 3000m, 500m, 300m, 100m, 500m, 50m)]));

        var fin1 = await svc.FinalizeAsync(report1.Id);
        var fin2 = await svc.FinalizeAsync(report2.Id);

        Assert.NotEqual(fin1.SignatureValue, fin2.SignatureValue);
    }

    // -----------------------------------------------------------------------
    // SignAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_RequiresFinalized_ThrowsOnDraft()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SignAsync(report.Id, "my-sig").AsTask());
    }

    [Fact]
    public async Task SignAsync_OnFinalized_MovesToSigned()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));
        var finalized = await svc.FinalizeAsync(report.Id);

        var signed = await svc.SignAsync(finalized.Id, "approved-by:chris");

        Assert.Equal(TaxReportStatus.Signed, signed.Status);
        Assert.Equal("approved-by:chris", signed.SignatureValue);
    }

    // -----------------------------------------------------------------------
    // AmendAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AmendAsync_RequiresSignedOrFinalized_ThrowsOnDraft()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AmendAsync(report.Id, "error in rents").AsTask());
    }

    [Fact]
    public async Task AmendAsync_OnSigned_SupersedesOriginalAndReturnsNewDraft()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));
        var finalized = await svc.FinalizeAsync(report.Id);
        var signed    = await svc.SignAsync(finalized.Id, "sig-v1");

        var amendment = await svc.AmendAsync(signed.Id, "Rent received was mis-keyed");

        Assert.Equal(TaxReportStatus.Draft, amendment.Status);
        Assert.NotEqual(report.Id, amendment.Id);

        // Original should now be Superseded.
        var original = await svc.GetAsync(report.Id);
        Assert.Equal(TaxReportStatus.Superseded, original!.Status);
    }

    [Fact]
    public async Task AmendAsync_OnFinalized_IsAllowed()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));
        var finalized = await svc.FinalizeAsync(report.Id);

        var amendment = await svc.AmendAsync(finalized.Id, "Correction before signing");

        Assert.Equal(TaxReportStatus.Draft, amendment.Status);
        var original = await svc.GetAsync(report.Id);
        Assert.Equal(TaxReportStatus.Superseded, original!.Status);
    }

    // -----------------------------------------------------------------------
    // State-guard violations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StateGuard_FinalizeOnFinalized_Throws()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));
        await svc.FinalizeAsync(report.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.FinalizeAsync(report.Id).AsTask());
    }

    // -----------------------------------------------------------------------
    // Concurrency
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentFinalizeAsync_Serialized_BothSucceedOrOneFails()
    {
        var svc = CreateService();
        var report = await svc.GenerateScheduleEAsync(
            new ScheduleEGenerationRequest(new TaxYear(2024),
                [new SchedulePropertyRow(MakePropertyId(), "1 Main", 1000m, 200m, 50m, 30m, 10m, 50m, 5m)]));

        // One call should win (Draft → Finalized); the second should throw InvalidOperationException
        // because the report is no longer Draft.
        var tasks = new[]
        {
            Task.Run(() => svc.FinalizeAsync(report.Id).AsTask()),
            Task.Run(() => svc.FinalizeAsync(report.Id).AsTask()),
        };

        var results = await Task.WhenAll(tasks.Select(t => t.ContinueWith(r => r)));
        var successes = results.Count(r => r.Status == TaskStatus.RanToCompletion);
        var failures  = results.Count(r => r.IsFaulted);

        Assert.Equal(1, successes);
        Assert.Equal(1, failures);
    }

    // -----------------------------------------------------------------------
    // TaxYear validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2019)]
    [InlineData(2101)]
    [InlineData(0)]
    [InlineData(-1)]
    public void TaxYear_OutOfRange_ThrowsArgumentOutOfRangeException(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaxYear(year));
    }

    [Theory]
    [InlineData(2020)]
    [InlineData(2024)]
    [InlineData(2100)]
    public void TaxYear_InRange_DoesNotThrow(int year)
    {
        var ty = new TaxYear(year);
        Assert.Equal(year, ty.Value);
    }
}

using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Blocks.TaxReporting.Rendering;
using Sunfish.Blocks.TaxReporting.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Tests;

public sealed class TaxReportTextRendererTests
{
    private static ITaxReportTextRenderer CreateRenderer() => new TaxReportTextRenderer();

    private static TaxReport MakeReport(TaxReportBody body, TaxReportKind kind)
        => new TaxReport(
            Id: TaxReportId.NewId(),
            Year: new TaxYear(2024),
            Kind: kind,
            PropertyId: null,
            Status: TaxReportStatus.Draft,
            GeneratedAtUtc: Instant.Now,
            SignatureValue: null,
            Body: body);

    private static EntityId MakePropertyId(string local = "prop1")
        => EntityId.Parse($"property:test/{local}");

    // -----------------------------------------------------------------------
    // Schedule E
    // -----------------------------------------------------------------------

    [Fact]
    public void ScheduleE_RendersAllPropertiesAndTotalsFooter()
    {
        var renderer = CreateRenderer();
        var rows = new[]
        {
            new SchedulePropertyRow(MakePropertyId("p1"), "123 Main Street",
                12000m, 4000m, 1200m, 600m, 300m, 800m, 100m),
            new SchedulePropertyRow(MakePropertyId("p2"), "456 Oak Avenue",
                18000m, 6000m, 1800m, 900m, 500m, 1200m, 200m),
        };
        var body = new ScheduleEBody(
            rows,
            TotalRents: 30000m,
            TotalExpenses: 17600m,
            NetIncomeOrLoss: 12400m);

        var output = renderer.Render(MakeReport(body, TaxReportKind.ScheduleE));

        Assert.Contains("SCHEDULE E", output);
        Assert.Contains("123 Main Street", output);
        Assert.Contains("456 Oak Avenue", output);
        Assert.Contains("TOTALS", output);
        // Check totals are present somewhere
        Assert.Contains("30,000", output); // TotalRents formatted as currency
        Assert.Contains("12,400", output); // NetIncomeOrLoss
    }

    // -----------------------------------------------------------------------
    // Form 1099-NEC
    // -----------------------------------------------------------------------

    [Fact]
    public void Form1099Nec_RendersAllRecipients()
    {
        var renderer = CreateRenderer();
        var recipients = new[]
        {
            new Nec1099Recipient("Alice Contractor", "XXX-XX-1234", "1 Main St", 1500m, "ACC-001"),
            new Nec1099Recipient("Carol Plumber",    "XXX-XX-9999", "3 Elm Rd",   800m),
        };
        var body = new Form1099NecBody(recipients);

        var output = renderer.Render(MakeReport(body, TaxReportKind.Form1099Nec));

        Assert.Contains("1099-NEC", output);
        Assert.Contains("Alice Contractor", output);
        Assert.Contains("1,500", output);
        Assert.Contains("ACC-001", output);
        Assert.Contains("Carol Plumber", output);
        Assert.Contains("800", output);
    }

    [Fact]
    public void Form1099Nec_NoRecipients_ShowsEmptyMessage()
    {
        var renderer = CreateRenderer();
        var body = new Form1099NecBody(Array.Empty<Nec1099Recipient>());

        var output = renderer.Render(MakeReport(body, TaxReportKind.Form1099Nec));

        Assert.Contains("No recipients", output);
    }

    // -----------------------------------------------------------------------
    // State Personal Property
    // -----------------------------------------------------------------------

    [Fact]
    public void StatePersonalProperty_RendersWithStateCodeHeader()
    {
        var renderer = CreateRenderer();
        var items = new[]
        {
            new PersonalPropertyRow("Office Computer", 2022, 1200m, 800m),
            new PersonalPropertyRow("Lawn Mower",      2020,  400m, 150m),
        };
        var body = new StatePersonalPropertyBody("WA", items);

        var output = renderer.Render(MakeReport(body, TaxReportKind.StatePersonalProperty));

        Assert.Contains("STATE PERSONAL PROPERTY", output);
        Assert.Contains("WA", output);
        Assert.Contains("Office Computer", output);
        Assert.Contains("Lawn Mower", output);
        Assert.Contains("1,200", output);
        Assert.Contains("800", output);
    }
}

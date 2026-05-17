using System.Text.Json;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillRecordTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Acct() => GLAccountId.NewId();
    private static PartyId Vendor() => PartyId.NewId();

    private static BillLine NewLine(BillId billId, int lineNo, decimal qty, decimal price) =>
        BillLine.Create(billId, lineNo, $"Line {lineNo}", qty, price, Acct());

    [Fact]
    public void Create_MaterializesSubtotalAndTotal()
    {
        var billId = BillId.NewId();
        var lines = new[]
        {
            NewLine(billId, 1, 2m, 50m),       // 100
            NewLine(billId, 2, 1.5m, 19.99m),  // 29.985 → banker's → 29.98
        };

        var bill = Bill.Create(
            tenantId: Tenant(),
            chartId: Chart(),
            billNumber: "VND-001",
            vendorId: Vendor(),
            billDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: lines,
            apAccountId: Acct(),
            id: billId);

        Assert.Equal(129.98m, bill.Subtotal);
        Assert.Equal(0m, bill.TaxTotal);
        Assert.Equal(129.98m, bill.Total);
        Assert.Equal(0m, bill.AmountPaid);
        Assert.Equal(129.98m, bill.Balance);
        Assert.Equal(BillStatus.Draft, bill.Status);
        Assert.Equal(1, bill.Version);
    }

    [Fact]
    public void Create_DefaultsToUsdAndDraftAndReceivedDateMatchesBillDate()
    {
        var billDate = new DateOnly(2026, 5, 1);
        var bill = Bill.Create(Tenant(), Chart(), "VND-002", Vendor(),
            billDate, new DateOnly(2026, 5, 31),
            Array.Empty<BillLine>(), Acct());

        Assert.Equal("USD", bill.Currency);
        Assert.Equal(BillStatus.Draft, bill.Status);
        Assert.Equal(billDate, bill.ReceivedDate); // defaults to bill date when null
        Assert.Equal(0m, bill.Total);
    }

    [Fact]
    public void Create_ReceivedDateOverride_ApplyAndTakesEffect()
    {
        var bill = Bill.Create(Tenant(), Chart(), "VND-003", Vendor(),
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31),
            Array.Empty<BillLine>(), Acct(),
            receivedDate: new DateOnly(2026, 5, 5));

        Assert.Equal(new DateOnly(2026, 5, 5), bill.ReceivedDate);
    }

    [Fact]
    public void IsOverdueAsOf_TrueOnlyWhenOpenPastDueWithBalance_DisputedExcluded()
    {
        var bill = Bill.Create(Tenant(), Chart(), "VND-OVERDUE", Vendor(),
            new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1),
            new[] { BillLine.Create(BillId.NewId(), 1, "x", 1m, 100m, Acct()) },
            Acct());

        // Draft: never overdue.
        Assert.False(bill.IsOverdueAsOf(new DateOnly(2026, 6, 1)));

        var received = bill with { Status = BillStatus.Received };
        Assert.False(received.IsOverdueAsOf(new DateOnly(2026, 4, 15))); // before due
        Assert.True(received.IsOverdueAsOf(new DateOnly(2026, 5, 2)));  // past due with balance

        var disputed = received with { Status = BillStatus.Disputed };
        Assert.False(disputed.IsOverdueAsOf(new DateOnly(2026, 6, 1))); // hold — NOT overdue

        var paid = received with { Status = BillStatus.Paid, AmountPaid = 100m, Balance = 0m };
        Assert.False(paid.IsOverdueAsOf(new DateOnly(2026, 6, 1))); // terminal

        var voided = received with { Status = BillStatus.Voided };
        Assert.False(voided.IsOverdueAsOf(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void BillStatus_JsonRoundtrip_AllSevenValuesUseLowercaseCamelCase()
    {
        Assert.Equal("\"draft\"",         JsonSerializer.Serialize(BillStatus.Draft));
        Assert.Equal("\"received\"",      JsonSerializer.Serialize(BillStatus.Received));
        Assert.Equal("\"approved\"",      JsonSerializer.Serialize(BillStatus.Approved));
        Assert.Equal("\"partiallyPaid\"", JsonSerializer.Serialize(BillStatus.PartiallyPaid));
        Assert.Equal("\"paid\"",          JsonSerializer.Serialize(BillStatus.Paid));
        Assert.Equal("\"voided\"",        JsonSerializer.Serialize(BillStatus.Voided));
        Assert.Equal("\"disputed\"",      JsonSerializer.Serialize(BillStatus.Disputed));

        Assert.Equal(BillStatus.PartiallyPaid, JsonSerializer.Deserialize<BillStatus>("\"partiallyPaid\""));
        Assert.Equal(BillStatus.Disputed,      JsonSerializer.Deserialize<BillStatus>("\"disputed\""));
    }

    [Fact]
    public void BillStatus_UnknownJson_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BillStatus>("\"overdue\""));
    }
}

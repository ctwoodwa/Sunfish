using System.Text.Json;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceRecordTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Acct() => GLAccountId.NewId();
    private static PartyId Customer() => PartyId.NewId();

    private static InvoiceLine NewLine(InvoiceId invoiceId, int lineNo, decimal qty, decimal price) =>
        InvoiceLine.Create(invoiceId, lineNo, $"Line {lineNo}", qty, price, Acct());

    [Fact]
    public void Create_MaterializesSubtotalAndTotal()
    {
        var invId = InvoiceId.NewId();
        var lines = new[]
        {
            NewLine(invId, 1, 2m, 50m),       // 100.00
            NewLine(invId, 2, 1.5m, 19.99m),  // 29.985 → round-half-to-even → 29.98
        };

        var inv = Invoice.Create(
            tenantId: Tenant(),
            chartId: Chart(),
            invoiceNumber: "INV-001",
            customerId: Customer(),
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: lines,
            arAccountId: Acct(),
            id: invId);

        Assert.Equal(129.98m, inv.Subtotal);
        Assert.Equal(0m, inv.TaxTotal);
        Assert.Equal(129.98m, inv.Total);
        Assert.Equal(0m, inv.AmountPaid);
        Assert.Equal(129.98m, inv.Balance);
        Assert.Equal(InvoiceStatus.Draft, inv.Status);
        Assert.Equal(1, inv.Version);
    }

    [Fact]
    public void Create_DefaultsToUsdAndDraft()
    {
        var inv = Invoice.Create(Tenant(), Chart(), "INV-002", Customer(),
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31),
            Array.Empty<InvoiceLine>(), Acct());

        Assert.Equal("USD", inv.Currency);
        Assert.Equal(InvoiceStatus.Draft, inv.Status);
        Assert.Equal(0m, inv.Total);
    }

    [Fact]
    public void IsOverdueAsOf_TrueOnlyWhenOpenPastDueWithBalance()
    {
        var inv = Invoice.Create(Tenant(), Chart(), "INV-003", Customer(),
            new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1),
            new[] { InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, 100m, Acct()) },
            Acct());

        // Draft: never overdue (it's not even Issued).
        Assert.False(inv.IsOverdueAsOf(new DateOnly(2026, 6, 1)));

        var issued = inv with { Status = InvoiceStatus.Issued };
        Assert.False(issued.IsOverdueAsOf(new DateOnly(2026, 4, 15))); // before due
        Assert.True(issued.IsOverdueAsOf(new DateOnly(2026, 5, 2)));  // past due, balance still 100

        var paid = issued with { Status = InvoiceStatus.Paid, AmountPaid = 100m, Balance = 0m };
        Assert.False(paid.IsOverdueAsOf(new DateOnly(2026, 6, 1)));   // terminal, no balance

        var voided = issued with { Status = InvoiceStatus.Voided };
        Assert.False(voided.IsOverdueAsOf(new DateOnly(2026, 6, 1))); // terminal
    }

    [Fact]
    public void InvoiceStatus_JsonRoundtrip_AllEnumValuesUseLowercaseCamelCase()
    {
        Assert.Equal("\"draft\"",         JsonSerializer.Serialize(InvoiceStatus.Draft));
        Assert.Equal("\"issued\"",        JsonSerializer.Serialize(InvoiceStatus.Issued));
        Assert.Equal("\"partiallyPaid\"", JsonSerializer.Serialize(InvoiceStatus.PartiallyPaid));
        Assert.Equal("\"paid\"",          JsonSerializer.Serialize(InvoiceStatus.Paid));
        Assert.Equal("\"voided\"",        JsonSerializer.Serialize(InvoiceStatus.Voided));
        Assert.Equal("\"writtenOff\"",    JsonSerializer.Serialize(InvoiceStatus.WrittenOff));

        Assert.Equal(InvoiceStatus.PartiallyPaid, JsonSerializer.Deserialize<InvoiceStatus>("\"partiallyPaid\""));
    }

    [Fact]
    public void InvoiceStatus_UnknownJson_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InvoiceStatus>("\"overdue\""));
    }
}

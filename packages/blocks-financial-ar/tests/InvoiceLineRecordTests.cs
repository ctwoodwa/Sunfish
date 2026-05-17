using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceLineRecordTests
{
    [Fact]
    public void Create_AmountIsBankerRoundedToTwoMinorUnits()
    {
        // 1.5 × 19.99 = 29.985 → ToEven → 29.98 (last-digit even).
        var line = InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1.5m, 19.99m, GLAccountId.NewId());
        Assert.Equal(29.98m, line.Amount);
    }

    [Fact]
    public void Create_PreservesFractionalQuantity()
    {
        // Defends against accidental int conversion of decimal quantity.
        var line = InvoiceLine.Create(InvoiceId.NewId(), 1, "hours", 2.25m, 100m, GLAccountId.NewId());
        Assert.Equal(2.25m, line.Quantity);
        Assert.Equal(225m, line.Amount);
    }

    [Fact]
    public void Create_LineWithNoTaxCode_DefaultsTaxAmountZero()
    {
        var line = InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, 50m, GLAccountId.NewId());
        Assert.Null(line.TaxCodeId);
        Assert.Equal(0m, line.TaxAmount);
    }

    [Fact]
    public void Create_LineWithTaxCodeId_KeepsItOpaque()
    {
        var line = InvoiceLine.Create(
            invoiceId: InvoiceId.NewId(),
            lineNumber: 1,
            description: "x",
            quantity: 1m,
            unitPrice: 100m,
            incomeAccountId: GLAccountId.NewId(),
            taxCodeId: "tx-tn-state-7pct"); // opaque string FK to blocks-financial-tax
        Assert.Equal("tx-tn-state-7pct", line.TaxCodeId);
        Assert.Equal(0m, line.TaxAmount); // populated by IInvoicePostingService (PR 3)
    }
}

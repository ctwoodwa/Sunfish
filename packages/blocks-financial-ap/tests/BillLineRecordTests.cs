using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillLineRecordTests
{
    [Fact]
    public void Create_AmountIsBankerRoundedToTwoMinorUnits()
    {
        // 1.5 × 19.99 = 29.985 → ToEven → 29.98 (last-digit even).
        var line = BillLine.Create(BillId.NewId(), 1, "x", 1.5m, 19.99m, GLAccountId.NewId());
        Assert.Equal(29.98m, line.Amount);
    }

    [Fact]
    public void Create_PreservesFractionalQuantity()
    {
        var line = BillLine.Create(BillId.NewId(), 1, "hours", 2.25m, 100m, GLAccountId.NewId());
        Assert.Equal(2.25m, line.Quantity);
        Assert.Equal(225m, line.Amount);
    }

    [Fact]
    public void Create_LineWithNoTaxCode_DefaultsTaxAmountZero()
    {
        var line = BillLine.Create(BillId.NewId(), 1, "x", 1m, 50m, GLAccountId.NewId());
        Assert.Null(line.TaxCodeId);
        Assert.Equal(0m, line.TaxAmount);
    }

    [Fact]
    public void Create_LineWithTaxCodeId_KeepsItOpaque()
    {
        var line = BillLine.Create(
            billId: BillId.NewId(),
            lineNumber: 1,
            description: "x",
            quantity: 1m,
            unitPrice: 100m,
            debitAccountId: GLAccountId.NewId(),
            taxCodeId: "tx-tn-state-7pct");
        Assert.Equal("tx-tn-state-7pct", line.TaxCodeId);
        Assert.Equal(0m, line.TaxAmount); // populated by IBillPostingService (PR 2)
    }
}

using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class ErpnextPurchaseInvoiceImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();
    private static PartyId Vendor() => PartyId.NewId();

    private sealed record Sut(ErpnextPurchaseInvoiceImporter Importer, InMemoryBillRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryBillRepository();
        return new Sut(new ErpnextPurchaseInvoiceImporter(repo), repo);
    }

    private static ErpnextPurchaseInvoiceSource NewSource(
        string name = "PINV-0001",
        string modified = "2026-05-17 12:00:00",
        string status = "Submitted",
        decimal grandTotal = 1000m,
        decimal outstanding = 1000m,
        string? billNo = "VND-INV-001",
        params ErpnextPurchaseInvoiceItem[] items) =>
        new(
            Name: name,
            Modified: modified,
            Supplier: "SUP-0001",
            BillNo: billNo,
            PostingDate: new DateOnly(2026, 5, 17),
            DueDate: new DateOnly(2026, 6, 17),
            BillDate: new DateOnly(2026, 5, 16),
            Currency: "USD",
            Items: items.Length > 0 ? items : new[]
            {
                new ErpnextPurchaseInvoiceItem("Consulting hours", 10m, 100m, 1000m),
            },
            Status: status,
            GrandTotal: grandTotal,
            OutstandingAmount: outstanding);

    // ── Insert path ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPurchaseInvoice_FreshSource_InsertsCanonicalBill()
    {
        var sut = NewSut();
        var source = NewSource();

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.NotNull(outcome.Entity);
        Assert.Equal(BillStatus.Received, outcome.Entity!.Status);
        Assert.Equal(1000m, outcome.Entity.Total);
        Assert.Equal(1000m, outcome.Entity.Balance);
        Assert.Equal("erpnext:pinv:PINV-0001", outcome.Entity.ExternalRef);
        Assert.Contains("erpnextModified:2026-05-17 12:00:00", outcome.Entity.Notes!);
        Assert.Equal("VND-INV-001", outcome.Entity.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_BlankBillNo_FallsBackToErpnextName()
    {
        var sut = NewSut();
        var source = NewSource(billNo: "");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal("PINV-0001", outcome.Entity!.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_DraftSource_BillNumberKept()
    {
        var sut = NewSut();
        var source = NewSource(status: "Draft");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal(BillStatus.Draft, outcome.Entity!.Status);
        Assert.Equal("VND-INV-001", outcome.Entity.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_MultipleLines_PreserveOrder()
    {
        var sut = NewSut();
        var source = NewSource(
            grandTotal: 1300m, outstanding: 1300m,
            items: new[]
            {
                new ErpnextPurchaseInvoiceItem("Item A", 2m, 500m, 1000m),
                new ErpnextPurchaseInvoiceItem("Item B", 3m, 100m, 300m),
            });

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal(2, outcome.Entity!.Lines.Count);
        Assert.Equal(1000m, outcome.Entity.Lines[0].Amount);
        Assert.Equal(300m, outcome.Entity.Lines[1].Amount);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_OutstandingLessThanGrandTotal_DerivesAmountPaidAndBalance()
    {
        var sut = NewSut();
        var source = NewSource(grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal(BillStatus.PartiallyPaid, outcome.Entity!.Status);
        Assert.Equal(600m, outcome.Entity.AmountPaid);
        Assert.Equal(400m, outcome.Entity.Balance);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullExpenseAccount_FallsBackToDefault()
    {
        var sut = NewSut();
        var defaultExpense = Account();
        var source = NewSource(
            items: new[] { new ErpnextPurchaseInvoiceItem("Hours", 1m, 500m, 500m, ExpenseAccount: null) },
            grandTotal: 500m, outstanding: 500m);

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), defaultExpense);

        Assert.Equal(defaultExpense, outcome.Entity!.Lines[0].DebitAccountId);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullBillDate_FallsBackToPostingDate()
    {
        var sut = NewSut();
        var source = NewSource() with { BillDate = null };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal(source.PostingDate, outcome.Entity!.BillDate);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullCurrency_DefaultsToUsd()
    {
        var sut = NewSut();
        var source = NewSource() with { Currency = null };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());

        Assert.Equal("USD", outcome.Entity!.Currency);
    }

    // ── Skipped / Updated paths ───────────────────────────────────────

    [Fact]
    public async Task UpsertPurchaseInvoice_SameModifiedKey_ReturnsSkipped()
    {
        var sut = NewSut();
        var chart = Chart();
        var vendor = Vendor();
        var ap = Account();
        var expense = Account();
        var source = NewSource();

        var first = await sut.Importer.UpsertPurchaseInvoiceAsync(source, Tenant(), chart, vendor, ap, expense);
        var second = await sut.Importer.UpsertPurchaseInvoiceAsync(source, Tenant(), chart, vendor, ap, expense);

        Assert.Equal(ImportOutcomeKind.Inserted, first.Kind);
        Assert.Equal(ImportOutcomeKind.Skipped, second.Kind);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_AdvancedModifiedKey_ReturnsUpdated()
    {
        var sut = NewSut();
        var chart = Chart();
        var vendor = Vendor();
        var ap = Account();
        var expense = Account();
        var v1 = NewSource(modified: "2026-05-17 12:00:00", grandTotal: 1000m, outstanding: 1000m);
        var v2 = NewSource(modified: "2026-05-18 09:00:00", grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var first = await sut.Importer.UpsertPurchaseInvoiceAsync(v1, Tenant(), chart, vendor, ap, expense);
        var second = await sut.Importer.UpsertPurchaseInvoiceAsync(v2, Tenant(), chart, vendor, ap, expense);

        Assert.Equal(ImportOutcomeKind.Updated, second.Kind);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
        Assert.Equal(BillStatus.PartiallyPaid, second.Entity.Status);
        Assert.Equal(600m, second.Entity.AmountPaid);
        Assert.Equal(2L, second.Entity.Version);
    }

    // ── Failed paths ──────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPurchaseInvoice_EmptyName_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Name = "" };
        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_EmptySupplier_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Supplier = "" };
        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NoItems_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Items = Array.Empty<ErpnextPurchaseInvoiceItem>() };
        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            source, Tenant(), Chart(), Vendor(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    // ── Status mapping ────────────────────────────────────────────────

    [Theory]
    [InlineData("Draft", BillStatus.Draft)]
    [InlineData("", BillStatus.Draft)]
    [InlineData(null, BillStatus.Draft)]
    [InlineData("Submitted", BillStatus.Received)]
    [InlineData("Overdue", BillStatus.Received)]
    [InlineData("Return", BillStatus.Received)]
    [InlineData("Debit Note Issued", BillStatus.Received)]
    [InlineData("Partly Paid", BillStatus.PartiallyPaid)]
    [InlineData("Partly Paid and Discounted", BillStatus.PartiallyPaid)]
    [InlineData("Paid", BillStatus.Paid)]
    [InlineData("Paid and Discounted", BillStatus.Paid)]
    [InlineData("Cancelled", BillStatus.Voided)]
    [InlineData("WhateverFuture", BillStatus.Draft)] // unknown → safest non-posting state
    public void MapStatus_HandlesAllKnownAndUnknownCodes(string? erpnext, BillStatus expected)
    {
        Assert.Equal(expected, ErpnextPurchaseInvoiceImporter.MapStatus(erpnext));
    }
}

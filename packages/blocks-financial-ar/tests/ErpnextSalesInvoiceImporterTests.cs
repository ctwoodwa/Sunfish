using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class ErpnextSalesInvoiceImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();
    private static PartyId Customer() => PartyId.NewId();

    private sealed record Sut(
        ErpnextSalesInvoiceImporter Importer,
        InMemoryInvoiceRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryInvoiceRepository();
        var numbering = new InMemoryInvoiceNumberingService(new ReplicaId("CW"));
        return new Sut(new ErpnextSalesInvoiceImporter(repo, numbering), repo);
    }

    private static ErpnextSalesInvoiceSource NewSource(
        string name = "SINV-0001",
        string modified = "2026-05-17 12:00:00",
        string status = "Submitted",
        decimal grandTotal = 1000m,
        decimal outstanding = 1000m,
        params ErpnextSalesInvoiceItem[] items) =>
        new(
            Name: name,
            Modified: modified,
            Customer: "CUST-0001",
            PostingDate: new DateOnly(2026, 5, 17),
            DueDate: new DateOnly(2026, 6, 17),
            Currency: "USD",
            Items: items.Length > 0 ? items : new[]
            {
                new ErpnextSalesInvoiceItem("Consulting hours", 10m, 100m, 1000m),
            },
            Status: status,
            GrandTotal: grandTotal,
            OutstandingAmount: outstanding);

    // ── Insert path ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertSalesInvoice_FreshSource_InsertsCanonicalInvoice()
    {
        var sut = NewSut();
        var source = NewSource();

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.NotNull(outcome.Entity);
        Assert.Equal(InvoiceStatus.Issued, outcome.Entity!.Status);
        Assert.Equal(1000m, outcome.Entity.Total);
        Assert.Equal(1000m, outcome.Entity.Balance);
        Assert.Equal("erpnext:sinv:SINV-0001", outcome.Entity.ExternalRef);
        Assert.Contains("erpnextModified:2026-05-17 12:00:00", outcome.Entity.Notes!);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NonDraft_MintsCanonicalInvoiceNumber()
    {
        var sut = NewSut();
        var source = NewSource(status: "Submitted");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.True(InvoiceNumberFormat.IsWellFormed(outcome.Entity!.InvoiceNumber));
    }

    [Fact]
    public async Task UpsertSalesInvoice_DraftSource_AcceptsEmptyInvoiceNumber()
    {
        var sut = NewSut();
        var source = NewSource(status: "Draft");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.Equal(InvoiceStatus.Draft, outcome.Entity!.Status);
        Assert.Equal("", outcome.Entity.InvoiceNumber);
    }

    [Fact]
    public async Task UpsertSalesInvoice_MultipleLines_PreservesOrderAndAmounts()
    {
        var sut = NewSut();
        var source = NewSource(
            grandTotal: 1300m, outstanding: 1300m,
            items: new[]
            {
                new ErpnextSalesInvoiceItem("Item A", 2m, 500m, 1000m),
                new ErpnextSalesInvoiceItem("Item B", 3m, 100m, 300m),
            });

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.Equal(2, outcome.Entity!.Lines.Count);
        Assert.Equal(1000m, outcome.Entity.Lines[0].Amount);
        Assert.Equal(300m, outcome.Entity.Lines[1].Amount);
        Assert.Equal("Item A", outcome.Entity.Lines[0].Description);
        Assert.Equal(1, outcome.Entity.Lines[0].LineNumber);
        Assert.Equal(2, outcome.Entity.Lines[1].LineNumber);
    }

    [Fact]
    public async Task UpsertSalesInvoice_OutstandingLessThanGrandTotal_DerivesAmountPaidAndBalance()
    {
        var sut = NewSut();
        var source = NewSource(grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.Equal(InvoiceStatus.PartiallyPaid, outcome.Entity!.Status);
        Assert.Equal(600m, outcome.Entity.AmountPaid);
        Assert.Equal(400m, outcome.Entity.Balance);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NullIncomeAccountOnItem_FallsBackToDefault()
    {
        var sut = NewSut();
        var defaultIncome = Account();
        var source = NewSource(items: new[]
        {
            new ErpnextSalesInvoiceItem("Hours", 1m, 500m, 500m, IncomeAccount: null),
        }, grandTotal: 500m, outstanding: 500m);

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), defaultIncome);

        Assert.Equal(defaultIncome, outcome.Entity!.Lines[0].IncomeAccountId);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NullCurrency_DefaultsToUsd()
    {
        var sut = NewSut();
        var source = NewSource() with { Currency = null };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());

        Assert.Equal("USD", outcome.Entity!.Currency);
    }

    // ── Skipped / Updated paths ───────────────────────────────────────

    [Fact]
    public async Task UpsertSalesInvoice_SameModifiedKey_ReturnsSkipped()
    {
        var sut = NewSut();
        var chart = Chart();
        var customer = Customer();
        var ar = Account();
        var income = Account();
        var source = NewSource();

        var first = await sut.Importer.UpsertSalesInvoiceAsync(source, Tenant(), chart, customer, ar, income);
        var second = await sut.Importer.UpsertSalesInvoiceAsync(source, Tenant(), chart, customer, ar, income);

        Assert.Equal(ImportOutcomeKind.Inserted, first.Kind);
        Assert.Equal(ImportOutcomeKind.Skipped, second.Kind);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
    }

    [Fact]
    public async Task UpsertSalesInvoice_AdvancedModifiedKey_ReturnsUpdated_WithNewState()
    {
        var sut = NewSut();
        var chart = Chart();
        var customer = Customer();
        var ar = Account();
        var income = Account();

        var v1 = NewSource(modified: "2026-05-17 12:00:00", grandTotal: 1000m, outstanding: 1000m);
        var v2 = NewSource(modified: "2026-05-18 09:00:00", grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var first = await sut.Importer.UpsertSalesInvoiceAsync(v1, Tenant(), chart, customer, ar, income);
        var second = await sut.Importer.UpsertSalesInvoiceAsync(v2, Tenant(), chart, customer, ar, income);

        Assert.Equal(ImportOutcomeKind.Updated, second.Kind);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id); // same canonical id (stable across update)
        Assert.Equal(InvoiceStatus.PartiallyPaid, second.Entity.Status);
        Assert.Equal(600m, second.Entity.AmountPaid);
        Assert.Equal(400m, second.Entity.Balance);
        Assert.Contains("erpnextModified:2026-05-18 09:00:00", second.Entity.Notes!);
        Assert.DoesNotContain("erpnextModified:2026-05-17 12:00:00", second.Entity.Notes!);
        Assert.Equal(2L, second.Entity.Version);
    }

    // ── Failed paths ──────────────────────────────────────────────────

    [Fact]
    public async Task UpsertSalesInvoice_EmptyName_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Name = "" };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
        Assert.Null(outcome.Entity);
    }

    [Fact]
    public async Task UpsertSalesInvoice_EmptyCustomer_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Customer = "" };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NoItems_ReturnsFailed()
    {
        var sut = NewSut();
        var source = NewSource() with { Items = Array.Empty<ErpnextSalesInvoiceItem>() };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            source, Tenant(), Chart(), Customer(), Account(), Account());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    // ── Status mapping ────────────────────────────────────────────────

    [Theory]
    [InlineData("Draft", InvoiceStatus.Draft)]
    [InlineData("", InvoiceStatus.Draft)]
    [InlineData(null, InvoiceStatus.Draft)]
    [InlineData("Submitted", InvoiceStatus.Issued)]
    [InlineData("Overdue", InvoiceStatus.Issued)]
    [InlineData("Return", InvoiceStatus.Issued)]
    [InlineData("Credit Note Issued", InvoiceStatus.Issued)]
    [InlineData("Partly Paid", InvoiceStatus.PartiallyPaid)]
    [InlineData("Partly Paid and Discounted", InvoiceStatus.PartiallyPaid)]
    [InlineData("Paid", InvoiceStatus.Paid)]
    [InlineData("Paid and Discounted", InvoiceStatus.Paid)]
    [InlineData("Cancelled", InvoiceStatus.Voided)]
    [InlineData("WhateverFuture", InvoiceStatus.Draft)] // unknown → safest non-posting state
    public void MapStatus_HandlesAllKnownAndUnknownCodes(string? erpnext, InvoiceStatus expected)
    {
        Assert.Equal(expected, ErpnextSalesInvoiceImporter.MapStatus(erpnext));
    }
}

using FL = Sunfish.Blocks.FinancialLedger.Models;
using LedgerMigration = Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 5 coverage for the ERPNext importer Pass 2 happy paths +
/// dedupe semantics. Not the primary tax-import path for Wave /
/// Rentler / Mac — exists per the migration-importer spec.
/// </summary>
public class ErpnextTaxImporterTests
{
    private sealed class Fx
    {
        public InMemoryAccountResolver Accounts { get; } = new();
        public InMemoryTaxCodeStore Codes { get; } = new();
        public InMemoryTaxRateLookup Rates { get; }
        public InMemoryTaxJurisdictionStore Jurisdictions { get; } = new();
        public InMemoryTaxJurisdictionResolver Resolver { get; }
        public ErpnextTaxImporter Importer { get; }
        public FL.GLAccount PayableAccount { get; }
        public FL.ChartOfAccountsId Chart { get; }

        public Fx()
        {
            Chart = FL.ChartOfAccountsId.NewId();
            PayableAccount = FL.GLAccount.Create(
                id: new FL.GLAccountId("erp-account-tax-payable"),
                chartId: Chart,
                code: "2200",
                name: "Tax payable",
                type: FL.GLAccountType.Liability,
                subtype: FL.AccountSubtype.TaxesPayable,
                currency: "USD");
            Accounts.Upsert(PayableAccount);
            Rates = new InMemoryTaxRateLookup(Accounts);
            Resolver = new InMemoryTaxJurisdictionResolver(Jurisdictions);
            Importer = new ErpnextTaxImporter(Codes, Rates, Resolver, Accounts);
        }
    }

    private static ErpnextTaxSource Source(
        string name,
        string modified = "2026-01-01 00:00:00",
        bool disabled = false,
        string taxName = "VA Sales Tax",
        params (string AccountHead, decimal Rate, bool Inclusive)[] rates)
    {
        var rows = rates.Length == 0
            ? new[] { (PayableId: "erp-account-tax-payable", Rate: 5m, Inclusive: false) }.Select(r => r).ToArray()
            : rates;
        var json = "[" + string.Join(",", rows.Select(r =>
            $"{{\"account_head\":\"{r.Item1}\",\"rate\":{r.Item2},\"included_in_print_rate\":{(r.Item3 ? "true" : "false")}}}")) + "]";
        return new ErpnextTaxSource(name, modified, taxName, json, disabled);
    }

    [Fact]
    public async Task Upsert_NewSource_InsertsTaxCodeAndRates()
    {
        var fx = new Fx();

        var outcome = await fx.Importer.UpsertFromErpnextAsync(
            Source("VA-Sales-001"), fx.Chart);

        Assert.Equal(LedgerMigration.ImportAction.Inserted, outcome.Action);
        Assert.NotNull(outcome.Record);
        Assert.Equal("VA Sales Tax", outcome.Record.Code);
        Assert.True(outcome.Record.IsActive);
        // Rate row also inserted.
        var rates = await fx.Rates.GetAllForTaxCodeAsync(outcome.Record.Id);
        Assert.Single(rates);
        Assert.Equal(5m, rates[0].RatePercent);
    }

    [Fact]
    public async Task Upsert_SameVersion_ReturnsSkipped()
    {
        var fx = new Fx();
        await fx.Importer.UpsertFromErpnextAsync(Source("VA-Sales-001"), fx.Chart);

        var again = await fx.Importer.UpsertFromErpnextAsync(Source("VA-Sales-001"), fx.Chart);

        Assert.Equal(LedgerMigration.ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task Upsert_TwoRateRows_CreatesTwoTaxRateRecords()
    {
        var fx = new Fx();
        var outcome = await fx.Importer.UpsertFromErpnextAsync(
            Source("VA-Sales-002",
                rates: new[] {
                    ("erp-account-tax-payable", 4m, false),
                    ("erp-account-tax-payable", 1m, false),
                }),
            fx.Chart);

        var rates = await fx.Rates.GetAllForTaxCodeAsync(outcome.Record.Id);
        Assert.Equal(2, rates.Count);
    }

    [Fact]
    public async Task Upsert_DisabledTrue_SetsTaxCodeIsActiveFalse()
    {
        var fx = new Fx();

        var outcome = await fx.Importer.UpsertFromErpnextAsync(
            Source("VA-Sales-003", disabled: true), fx.Chart);

        Assert.False(outcome.Record.IsActive);
    }
}

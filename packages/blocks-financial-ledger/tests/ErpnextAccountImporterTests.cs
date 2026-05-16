using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 6 — coverage for <see cref="ErpnextAccountImporter"/>
/// idempotency + enum-mapping invariants.
/// </summary>
public sealed class ErpnextAccountImporterTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task UpsertAccount_NewAccount_ReturnsInserted()
    {
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        var source = NewBankSource("acc-001", "2026-05-16 12:00:00");

        var outcome = await sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal(GLAccountType.Asset, outcome.Record.Type);
        Assert.Equal(AccountSubtype.BankAccount, outcome.Record.Subtype);
        Assert.Equal("acc-001", outcome.Record.ExternalRef);
    }

    [Fact]
    public async Task UpsertAccount_SameVersion_ReturnsSkipped()
    {
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        var source = NewBankSource("acc-001", "2026-05-16 12:00:00");
        await sut.UpsertFromErpnextAsync(source, Chart);

        var again = await sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task UpsertAccount_NewerVersion_ReturnsUpdated()
    {
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        var v1 = NewBankSource("acc-001", "2026-05-16 12:00:00");
        await sut.UpsertFromErpnextAsync(v1, Chart);

        var v2 = v1 with { Modified = "2026-05-17 09:00:00", AccountName = "Renamed Operating Bank" };
        var outcome = await sut.UpsertFromErpnextAsync(v2, Chart);

        Assert.Equal(ImportAction.Updated, outcome.Action);
        Assert.Equal("Renamed Operating Bank", outcome.Record.Name);
    }

    [Fact]
    public async Task UpsertAccount_MapsGroupAccountAsNonPostable()
    {
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        var source = new ErpnextAccountSource(
            Name: "acc-group", Modified: "2026-05-16",
            AccountName: "Assets",
            AccountNumber: "1000", ParentAccountName: null, AccountType: null,
            IsGroup: true, Disabled: false);

        var outcome = await sut.UpsertFromErpnextAsync(source, Chart);

        Assert.False(outcome.Record.IsPostable);
    }

    [Fact]
    public async Task UpsertAccount_MapsDisabledAsInactive()
    {
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        var source = NewBankSource("acc-d", "2026-05-16") with { Disabled = true };

        var outcome = await sut.UpsertFromErpnextAsync(source, Chart);

        Assert.False(outcome.Record.IsActive);
    }

    [Fact]
    public async Task UpsertAccount_ParentResolvesByExternalRef()
    {
        // Topological ordering — import parent first, then child.
        var resolver = new InMemoryAccountResolver();
        var sut = new ErpnextAccountImporter(resolver);
        await sut.UpsertFromErpnextAsync(
            new ErpnextAccountSource("acc-parent", "2026-05-16",
                "Current Assets", "1100", null, null, IsGroup: true, Disabled: false),
            Chart);

        var child = new ErpnextAccountSource("acc-child", "2026-05-16",
            "Operating Bank", "1110", ParentAccountName: "acc-parent",
            AccountType: "Bank", IsGroup: false, Disabled: false);
        var outcome = await sut.UpsertFromErpnextAsync(child, Chart);

        Assert.NotNull(outcome.Record.ParentAccountId);
    }

    [Theory]
    [InlineData("Bank",            GLAccountType.Asset,    AccountSubtype.BankAccount)]
    [InlineData("Receivable",      GLAccountType.Asset,    AccountSubtype.AccountsReceivable)]
    [InlineData("Income Account",  GLAccountType.Revenue,  AccountSubtype.OperatingIncome)]
    [InlineData("Expense Account", GLAccountType.Expense,  AccountSubtype.OperatingExpense)]
    [InlineData("Payable",         GLAccountType.Liability, AccountSubtype.AccountsPayable)]
    [InlineData("Equity",          GLAccountType.Equity,   AccountSubtype.OwnersEquity)]
    public void MapAccountType_ReturnsExpectedPair(
        string accountType, GLAccountType type, AccountSubtype subtype)
    {
        var (t, s) = ErpnextAccountImporter.MapAccountType(accountType);
        Assert.Equal(type, t);
        Assert.Equal(subtype, s);
    }

    private static ErpnextAccountSource NewBankSource(string name, string modified) =>
        new(Name: name, Modified: modified,
            AccountName: "Operating Bank Account",
            AccountNumber: "1110",
            ParentAccountName: null,
            AccountType: "Bank",
            IsGroup: false,
            Disabled: false);
}

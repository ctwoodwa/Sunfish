using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 6 — coverage for <see cref="ErpnextJournalEntryImporter"/>
/// idempotency + voucher-type mapping + opening-balance override.
/// </summary>
public sealed class ErpnextJournalEntryImporterTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task UpsertJE_NewEntry_InsertsAndReturnsInserted()
    {
        var h = new Harness();
        var source = h.NewBalancedSource("je-001", "Manual JE", "Journal Entry");

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.NotNull(outcome.Record);
        Assert.Equal(JournalEntryStatus.Posted, outcome.Record.Status);
        Assert.Single(h.Store.Snapshot());
    }

    [Fact]
    public async Task UpsertJE_ExistingEntry_ReturnsSkipped()
    {
        var h = new Harness();
        var source = h.NewBalancedSource("je-001", "first", "Journal Entry");
        await h.Sut.UpsertFromErpnextAsync(source, Chart);

        var again = await h.Sut.UpsertFromErpnextAsync(source with { Memo = "second" }, Chart);

        Assert.Equal(ImportAction.Skipped, again.Action);
        // Skipped does NOT add another entry to the store.
        Assert.Single(h.Store.Snapshot());
    }

    [Fact]
    public async Task UpsertJE_OpeningBalance_SetsSourceKindMigration()
    {
        var h = new Harness();
        // VoucherType=Journal Entry which would normally map to Manual,
        // but IsOpening=true must override to Migration.
        var source = h.NewBalancedSource("je-001", "Opening", "Journal Entry") with { IsOpening = true };

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal(JournalEntrySource.Migration, outcome.Record.SourceKind);
    }

    [Fact]
    public async Task UpsertJE_UnknownAccount_ReturnsSkipped()
    {
        var h = new Harness();
        var unknown = "non-existent-account";
        var source = h.NewBalancedSource("je-001", "Bad account ref", "Journal Entry") with
        {
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource(unknown, 100m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 100m, null, null),
            },
        };

        var outcome = await h.Sut.UpsertFromErpnextAsync(source, Chart);

        Assert.Equal(ImportAction.Skipped, outcome.Action);
        Assert.NotNull(outcome.Detail);
        Assert.Contains(unknown, outcome.Detail!);
        Assert.Empty(h.Store.Snapshot());
    }

    [Theory]
    [InlineData("Opening Entry",      JournalEntrySource.Migration)]
    [InlineData("Bank Entry",         JournalEntrySource.Payment)]
    [InlineData("Cash Entry",         JournalEntrySource.Payment)]
    [InlineData("Depreciation Entry", JournalEntrySource.Depreciation)]
    [InlineData("Journal Entry",      JournalEntrySource.Manual)]
    public void MapVoucherType_ReturnsExpectedSource(string voucherType, JournalEntrySource expected)
    {
        Assert.Equal(expected, ErpnextJournalEntryImporter.MapVoucherType(voucherType));
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryAccountResolver Accounts { get; }
        public InMemoryJournalStore Store { get; } = new();
        public GLAccount AccountA { get; }
        public GLAccount AccountB { get; }
        public string AccountAExtRef => "acc-A";
        public string AccountBExtRef => "acc-B";
        public ErpnextJournalEntryImporter Sut { get; }

        public Harness()
        {
            AccountA = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "1110", name: "Bank",
                type: GLAccountType.Asset, subtype: AccountSubtype.BankAccount, currency: "USD",
                externalRef: AccountAExtRef);
            AccountB = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "4100", name: "Rent",
                type: GLAccountType.Revenue, subtype: AccountSubtype.OperatingIncome, currency: "USD",
                externalRef: AccountBExtRef);
            Accounts = new InMemoryAccountResolver(new[] { AccountA, AccountB });
            var periods = new InMemoryPeriodResolver();
            var user = new StaticUserContext("importer", new[] { "FinancialAdmin" });
            var posting = new JournalPostingService(Accounts, periods, Store, user, TimeProvider.System);
            Sut = new ErpnextJournalEntryImporter(Accounts, posting, Store);
        }

        public ErpnextJournalEntrySource NewBalancedSource(string name, string memo, string voucher) =>
            new(Name: name,
                Modified: "2026-05-16 12:00:00",
                PostingDate: new DateOnly(2026, 5, 16),
                Memo: memo,
                VoucherType: voucher,
                IsOpening: false,
                DocStatus: 1,
                Lines: new[]
                {
                    new ErpnextJournalEntryLineSource(AccountAExtRef, 100m, 0m, null, null),
                    new ErpnextJournalEntryLineSource(AccountBExtRef, 0m, 100m, null, null),
                });
    }
}

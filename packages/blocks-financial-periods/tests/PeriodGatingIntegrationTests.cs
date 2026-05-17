using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — round-trip integration test per the PR 2 hand-off
/// verification step: post a JournalEntry against a chart with an Open
/// period → succeeds; soft-close the period via
/// <see cref="PeriodCloseService"/> → next post for a non-admin returns
/// <see cref="PostError.PeriodSoftClosed"/>; admin post still succeeds.
/// Exercises the full chain (resolver projects from repo into the
/// ledger's snapshot contract; ledger gates accordingly).
/// </summary>
public sealed class PeriodGatingIntegrationTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task RoundTrip_OpenThenSoftCloseThenAdminBypass()
    {
        // Storage seams.
        var periodRepo = new InMemoryFiscalPeriodRepository();
        var yearRepo   = new InMemoryFiscalYearRepository();
        var events     = new NoopDomainEventPublisher();

        // Seed a fiscal year + a monthly period covering today.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, $"{today.Year}",
            new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31));
        await yearRepo.InsertAsync(fy);

        var period = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, fy.Id,
            FiscalPeriodKind.Monthly, $"{today.Year}-M{today.Month:00}",
            new DateOnly(today.Year, today.Month, 1),
            new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));
        await periodRepo.InsertAsync(period);

        // Wire the ledger posting service against the production
        // resolver (SqlitePeriodResolver-via-repo).
        var resolver  = new SqlitePeriodResolver(periodRepo);
        var accountA  = GLAccount.Create(
            id: GLAccountId.NewId(), chartId: Chart, code: "1100", name: "Cash",
            type: GLAccountType.Asset, subtype: AccountSubtype.BankAccount, currency: "USD");
        var accountB  = GLAccount.Create(
            id: GLAccountId.NewId(), chartId: Chart, code: "4100", name: "Rent",
            type: GLAccountType.Revenue, subtype: AccountSubtype.OperatingIncome, currency: "USD");
        var accounts  = new InMemoryAccountResolver(new[] { accountA, accountB });
        var store     = new InMemoryJournalStore();
        var nonAdmin  = new StaticUserContext("user-1", roles: null);
        var admin     = new StaticUserContext("admin-1", roles: new[] { "FinancialAdmin" });
        var nonAdminPost = new JournalPostingService(accounts, resolver, store, nonAdmin, TimeProvider.System);
        var adminPost    = new JournalPostingService(accounts, resolver, store, admin,    TimeProvider.System);

        JournalEntry NewEntry() => new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: today,
            memo: "round-trip",
            lines: new[]
            {
                new JournalEntryLine(accountA.Id, debit: 100m, credit: 0m),
                new JournalEntryLine(accountB.Id, debit: 0m,   credit: 100m),
            },
            createdAtUtc: Instant.Now) with { ChartId = Chart };

        // 1) Open period — non-admin post succeeds.
        var first = await nonAdminPost.PostAsync(NewEntry());
        Assert.True(first.IsSuccess, first.Detail);

        // 2) Soft-close the period via PeriodCloseService.
        var closeSvc = new PeriodCloseService(periodRepo, yearRepo, events, TimeProvider.System);
        var closeResult = await closeSvc.SoftCloseAsync(period.Id);
        Assert.True(closeResult.IsSuccess, closeResult.Detail);
        Assert.Equal(FiscalPeriodStatus.SoftClosed, closeResult.Period!.Status);

        // 3) Soft-closed period — non-admin post rejected with
        //    PostError.PeriodSoftClosed (the gating contract).
        var rejected = await nonAdminPost.PostAsync(NewEntry());
        Assert.False(rejected.IsSuccess);
        Assert.Equal(PostError.PeriodSoftClosed, rejected.Error);

        // 4) Soft-closed period — admin post still succeeds.
        var adminPosted = await adminPost.PostAsync(NewEntry());
        Assert.True(adminPosted.IsSuccess, adminPosted.Detail);
    }
}

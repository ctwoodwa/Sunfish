using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 2 — coverage for the <see cref="ChartOfAccounts"/> record
/// per Stage 02 §3.2. Light contract tests; full registration / lookup
/// validation ships with PR 5 (`InMemoryAccountingService` extensions).
/// </summary>
public sealed class ChartOfAccountsTests
{
    [Fact]
    public void Construction_PreservesAllFields()
    {
        var id = ChartOfAccountsId.NewId();
        var legal = LegalEntityId.NewId();
        var retained = GLAccountId.NewId();
        var now = Instant.Now;

        var chart = new ChartOfAccounts(
            Id:                          id,
            LegalEntityId:               legal,
            Name:                        "Acero Properties LLC — Operating",
            BaseCurrency:                "USD",
            FiscalYearStartMonth:        1,
            FiscalYearStartDay:          1,
            RetainedEarningsAccountId:   retained,
            IsActive:                    true,
            CreatedAtUtc:                now,
            UpdatedAtUtc:                now);

        Assert.Equal(id, chart.Id);
        Assert.Equal(legal, chart.LegalEntityId);
        Assert.Equal("Acero Properties LLC — Operating", chart.Name);
        Assert.Equal("USD", chart.BaseCurrency);
        Assert.Equal(1, chart.FiscalYearStartMonth);
        Assert.Equal(1, chart.FiscalYearStartDay);
        Assert.Equal(retained, chart.RetainedEarningsAccountId);
        Assert.True(chart.IsActive);
        Assert.Equal(now, chart.CreatedAtUtc);
        Assert.Equal(now, chart.UpdatedAtUtc);
    }

    [Fact]
    public void RetainedEarningsAccountId_IsOptional()
    {
        var chart = new ChartOfAccounts(
            Id:                          ChartOfAccountsId.NewId(),
            LegalEntityId:               LegalEntityId.NewId(),
            Name:                        "test",
            BaseCurrency:                "EUR",
            FiscalYearStartMonth:        4,
            FiscalYearStartDay:          1,
            RetainedEarningsAccountId:   null,
            IsActive:                    true,
            CreatedAtUtc:                Instant.Now,
            UpdatedAtUtc:                Instant.Now);

        Assert.Null(chart.RetainedEarningsAccountId);
    }
}

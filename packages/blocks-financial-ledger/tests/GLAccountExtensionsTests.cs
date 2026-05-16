using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 2 — coverage for the <see cref="GLAccount"/> Stage 02 §3.1
/// schema extensions: derived NormalBalance via <see cref="GLAccount.Create"/>,
/// parent/chart consistency invariants via <see cref="GLAccount.Validate"/>,
/// and the back-compat guarantee on the original positional record
/// constructor.
/// </summary>
public sealed class GLAccountExtensionsTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public void Create_DerivesNormalBalanceFromType_Asset()
    {
        var acct = GLAccount.Create(
            GLAccountId.NewId(), Chart, "1000", "Cash",
            GLAccountType.Asset, AccountSubtype.BankAccount, "USD");
        Assert.Equal(NormalBalance.Debit, acct.NormalBalance);
    }

    [Fact]
    public void Create_DerivesNormalBalanceFromType_Liability()
    {
        var acct = GLAccount.Create(
            GLAccountId.NewId(), Chart, "2000", "Accounts Payable",
            GLAccountType.Liability, AccountSubtype.AccountsPayable, "USD");
        Assert.Equal(NormalBalance.Credit, acct.NormalBalance);
    }

    [Fact]
    public void Create_DerivesNormalBalanceFromType_Equity()
    {
        var acct = GLAccount.Create(
            GLAccountId.NewId(), Chart, "3000", "Owner's Equity",
            GLAccountType.Equity, AccountSubtype.OwnersEquity, "USD");
        Assert.Equal(NormalBalance.Credit, acct.NormalBalance);
    }

    [Fact]
    public void Create_DerivesNormalBalanceFromType_Revenue()
    {
        var acct = GLAccount.Create(
            GLAccountId.NewId(), Chart, "4000", "Rental Revenue",
            GLAccountType.Revenue, AccountSubtype.OperatingIncome, "USD");
        Assert.Equal(NormalBalance.Credit, acct.NormalBalance);
    }

    [Fact]
    public void Create_DerivesNormalBalanceFromType_Expense()
    {
        var acct = GLAccount.Create(
            GLAccountId.NewId(), Chart, "5000", "Operating Expenses",
            GLAccountType.Expense, AccountSubtype.OperatingExpense, "USD");
        Assert.Equal(NormalBalance.Debit, acct.NormalBalance);
    }

    [Fact]
    public void Validate_ReturnsError_WhenParentTypeMismatch()
    {
        var parent = GLAccount.Create(
            GLAccountId.NewId(), Chart, "1000", "Assets Root",
            GLAccountType.Asset, AccountSubtype.OtherAsset, "USD");
        var child = GLAccount.Create(
            GLAccountId.NewId(), Chart, "5000", "Expenses Misplaced",
            GLAccountType.Expense, AccountSubtype.OperatingExpense, "USD",
            parentAccountId: parent.Id);

        var result = child.Validate(parent);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Type") && e.Contains("Asset") && e.Contains("Expense"));
    }

    [Fact]
    public void Validate_ReturnsError_WhenParentChartMismatch()
    {
        var chartB = ChartOfAccountsId.NewId();
        var parent = GLAccount.Create(
            GLAccountId.NewId(), Chart, "1000", "Assets Root",
            GLAccountType.Asset, AccountSubtype.OtherAsset, "USD");
        var child = GLAccount.Create(
            GLAccountId.NewId(), chartB, "1100", "Cash (other chart)",
            GLAccountType.Asset, AccountSubtype.BankAccount, "USD",
            parentAccountId: parent.Id);

        var result = child.Validate(parent);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ChartId"));
    }

    [Fact]
    public void Validate_Succeeds_OnWellFormedAccount()
    {
        var parent = GLAccount.Create(
            GLAccountId.NewId(), Chart, "1000", "Current Assets",
            GLAccountType.Asset, AccountSubtype.CurrentAsset, "USD");
        var child = GLAccount.Create(
            GLAccountId.NewId(), Chart, "1010", "Operating Bank",
            GLAccountType.Asset, AccountSubtype.BankAccount, "USD",
            parentAccountId: parent.Id);

        var result = child.Validate(parent);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsError_OnInvalidCurrency()
    {
        var acct = new GLAccount(
            Id: GLAccountId.NewId(),
            Code: "1000",
            Name: "Bad currency",
            Type: GLAccountType.Asset,
            ParentAccountId: null,
            ChartId: Chart,
            Subtype: AccountSubtype.BankAccount,
            NormalBalance: NormalBalance.Debit,
            Currency: "USDOLLAR");

        var result = acct.Validate();
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ISO 4217"));
    }

    [Fact]
    public void Validate_AllowsContraBalanceSubtype()
    {
        // AccumulatedDepreciation is an Asset with Credit-normal balance —
        // the validation must permit it without flagging the type/normal
        // mismatch.
        var acct = new GLAccount(
            Id: GLAccountId.NewId(),
            Code: "1500",
            Name: "Accumulated Depreciation — Buildings",
            Type: GLAccountType.Asset,
            ParentAccountId: null,
            ChartId: Chart,
            Subtype: AccountSubtype.AccumulatedDepreciation,
            NormalBalance: NormalBalance.Credit,
            Currency: "USD");

        var result = acct.Validate();
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Constructor_BackCompat_WithOnlyOriginalArgs()
    {
        // Pre-PR-2 call sites construct GLAccount with only the original
        // 5 positional args. The PR 2 extensions must not break those.
        var acct = new GLAccount(
            Id: GLAccountId.NewId(),
            Code: "1000",
            Name: "Cash",
            Type: GLAccountType.Asset);

        Assert.True(acct.IsActive);
        Assert.True(acct.IsPostable);
        Assert.Null(acct.ChartId);
        Assert.Null(acct.Subtype);
        Assert.Null(acct.NormalBalance);
    }
}

using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Seeds;

/// <summary>
/// Catalogue of pre-built <see cref="ChartTemplate"/> shapes. Used by
/// <see cref="Services.IChartSeedingService"/> at chart-creation time
/// to populate a fresh <see cref="ChartOfAccounts"/> with a sensible
/// starter set of <see cref="GLAccount"/> records.
/// </summary>
public static class DefaultChartTemplates
{
    /// <summary>
    /// Property-management chart shape tuned for a US single-LLC rental
    /// business with Schedule E (Form 1040) reporting. Expense rows
    /// carry Schedule E line numbers in <see cref="ChartTemplateAccount.Code"/>
    /// comments to bind them to the eventual
    /// <c>blocks-reports-tax.TaxFormLineMap</c> records (NOT seeded here
    /// — that ships with a separate <c>blocks-reports-*</c> hand-off).
    /// </summary>
    public static readonly ChartTemplate RentalRealEstate = new(
        Name: "Rental Real Estate (US, single LLC)",
        Description: "Suitable for a property LLC with rental income, operating "
                   + "expenses, and Schedule E (Form 1040) reporting needs.",
        Accounts: new ChartTemplateAccount[]
        {
            // 1xxx Assets
            new("1000", "Assets", GLAccountType.Asset, AccountSubtype.OtherAsset, IsPostable: false),
            new("1100", "Current Assets", GLAccountType.Asset, AccountSubtype.CurrentAsset, ParentCode: "1000", IsPostable: false),
            new("1110", "Operating Bank Account", GLAccountType.Asset, AccountSubtype.BankAccount, ParentCode: "1100"),
            new("1120", "Security Deposit Holding (Bank)", GLAccountType.Asset, AccountSubtype.BankAccount, ParentCode: "1100"),
            new("1130", "Accounts Receivable", GLAccountType.Asset, AccountSubtype.AccountsReceivable, ParentCode: "1100"),
            new("1500", "Fixed Assets", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1000", IsPostable: false),
            new("1510", "Buildings", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new("1520", "Land", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new("1530", "Equipment", GLAccountType.Asset, AccountSubtype.FixedAsset, ParentCode: "1500"),
            new("1590", "Accumulated Depreciation", GLAccountType.Asset, AccountSubtype.AccumulatedDepreciation, ParentCode: "1500"),

            // 2xxx Liabilities
            new("2000", "Liabilities", GLAccountType.Liability, AccountSubtype.OtherLiability, IsPostable: false),
            new("2100", "Current Liabilities", GLAccountType.Liability, AccountSubtype.CurrentLiability, ParentCode: "2000", IsPostable: false),
            new("2110", "Accounts Payable", GLAccountType.Liability, AccountSubtype.AccountsPayable, ParentCode: "2100"),
            new("2120", "Security Deposits Held", GLAccountType.Liability, AccountSubtype.CurrentLiability, ParentCode: "2100"),
            new("2130", "Sales Tax Payable", GLAccountType.Liability, AccountSubtype.TaxesPayable, ParentCode: "2100"),
            new("2500", "Long-Term Liabilities", GLAccountType.Liability, AccountSubtype.LongTermLiability, ParentCode: "2000", IsPostable: false),
            new("2510", "Mortgages Payable", GLAccountType.Liability, AccountSubtype.LongTermLiability, ParentCode: "2500"),

            // 3xxx Equity
            new("3000", "Equity", GLAccountType.Equity, AccountSubtype.OwnersEquity, IsPostable: false),
            new("3100", "Owner's Capital", GLAccountType.Equity, AccountSubtype.OwnersEquity, ParentCode: "3000"),
            new("3200", "Owner's Drawings", GLAccountType.Equity, AccountSubtype.Drawings, ParentCode: "3000"),
            new("3900", "Retained Earnings", GLAccountType.Equity, AccountSubtype.RetainedEarnings, ParentCode: "3000"),

            // 4xxx Revenue
            new("4000", "Revenue", GLAccountType.Revenue, AccountSubtype.OperatingIncome, IsPostable: false),
            new("4100", "Rental Income", GLAccountType.Revenue, AccountSubtype.OperatingIncome, ParentCode: "4000"),
            new("4200", "Late Fee Income", GLAccountType.Revenue, AccountSubtype.OperatingIncome, ParentCode: "4000"),
            new("4900", "Other Income", GLAccountType.Revenue, AccountSubtype.OtherIncome, ParentCode: "4000"),

            // 5xxx-7xxx Expenses (Schedule E line-mapped — see ScheduleELineMap below)
            new("5000", "Expenses", GLAccountType.Expense, AccountSubtype.OperatingExpense, IsPostable: false),
            new("5100", "Advertising", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                  // Schedule E Line 5
            new("5200", "Cleaning and Maintenance", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),    // Line 7
            new("5300", "Insurance", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                   // Line 9
            new("5400", "Legal and Professional Fees", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"), // Line 10
            new("5500", "Management Fees", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),             // Line 11
            new("5600", "Repairs", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                     // Line 14
            new("5700", "Supplies", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                    // Line 15
            new("5800", "Utilities", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                   // Line 17
            new("6100", "Property Tax", GLAccountType.Expense, AccountSubtype.OperatingExpense, ParentCode: "5000"),                // Line 16
            new("7110", "Mortgage Interest", GLAccountType.Expense, AccountSubtype.InterestExpense, ParentCode: "5000"),            // Line 12
            new("7200", "Depreciation Expense", GLAccountType.Expense, AccountSubtype.DepreciationExpense, ParentCode: "5000"),     // Line 18
        });

    /// <summary>
    /// Schedule E (Form 1040) line → <see cref="ChartTemplateAccount.Code"/>
    /// binding for the <see cref="RentalRealEstate"/> template. Used by
    /// the future <c>blocks-reports-tax</c> hand-off to seed
    /// <c>TaxFormLineMap</c> records and by the package's
    /// DefaultChartTemplatesTests to verify coverage.
    /// </summary>
    /// <remarks>
    /// The template intentionally does NOT cover every Schedule E line
    /// (lines 6 Auto / 8 Commissions / 13 Other are omitted as
    /// blank-rare for a property LLC). The test verifies the listed
    /// lines map to a present account, not universal Schedule E
    /// coverage.
    /// </remarks>
    public static readonly IReadOnlyDictionary<int, string> RentalRealEstateScheduleELineMap =
        new Dictionary<int, string>
        {
            [5]  = "5100", // Advertising
            [7]  = "5200", // Cleaning and Maintenance
            [9]  = "5300", // Insurance
            [10] = "5400", // Legal and Professional Fees
            [11] = "5500", // Management Fees
            [12] = "7110", // Mortgage Interest
            [14] = "5600", // Repairs
            [15] = "5700", // Supplies
            [16] = "6100", // Property Tax
            [17] = "5800", // Utilities
            [18] = "7200", // Depreciation
        };
}

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Sub-classification under <see cref="GLAccountType"/> per Stage 02
/// <c>blocks-financial-schema-design.md</c> §3.1. Subtype drives
/// presentation grouping on financial statements (Balance Sheet,
/// Income Statement) without changing the high-level
/// debit/credit normal balance derived from the parent
/// <see cref="GLAccountType"/>.
/// </summary>
public enum AccountSubtype
{
    // Assets
    CurrentAsset,
    FixedAsset,
    BankAccount,
    AccountsReceivable,
    InventoryAsset,
    AccumulatedDepreciation,
    OtherAsset,

    // Liabilities
    CurrentLiability,
    AccountsPayable,
    LongTermLiability,
    TaxesPayable,
    OtherLiability,

    // Equity
    OwnersEquity,
    RetainedEarnings,
    Drawings,

    // Income
    OperatingIncome,
    OtherIncome,

    // Expense
    OperatingExpense,
    CostOfGoodsSold,
    InterestExpense,
    DepreciationExpense,
    OtherExpense,
}

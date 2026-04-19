namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// Classifies a general ledger account into one of the five standard accounting categories.
/// Drives how balances are displayed and how the accounting equation (Assets = Liabilities + Equity) is maintained.
/// </summary>
public enum GLAccountType
{
    /// <summary>Resources owned by the entity (e.g. cash, receivables, property).</summary>
    Asset,

    /// <summary>Obligations owed to creditors (e.g. accounts payable, loans).</summary>
    Liability,

    /// <summary>Residual interest of owners after liabilities (e.g. retained earnings, capital).</summary>
    Equity,

    /// <summary>Income earned from operations (e.g. rental income, service fees).</summary>
    Revenue,

    /// <summary>Costs incurred in generating revenue (e.g. maintenance, utilities).</summary>
    Expense,
}

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// A single node in the general ledger chart of accounts.
/// </summary>
/// <param name="Id">Unique account identifier.</param>
/// <param name="Code">
/// Human-readable account code, e.g. <c>"4000"</c> for Rental Revenue.
/// Codes must be unique within a chart of accounts.
/// </param>
/// <param name="Name">Display name of the account, e.g. <c>"Rental Revenue"</c>.</param>
/// <param name="Type">Accounting category (Asset, Liability, Equity, Revenue, Expense).</param>
/// <param name="ParentAccountId">
/// Optional reference to a parent account, enabling hierarchical chart-of-accounts structures.
/// <see langword="null"/> for top-level accounts.
/// </param>
public sealed record GLAccount(
    GLAccountId Id,
    string Code,
    string Name,
    GLAccountType Type,
    GLAccountId? ParentAccountId = null);

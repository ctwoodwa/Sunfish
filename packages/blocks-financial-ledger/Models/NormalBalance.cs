namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// The side a <see cref="GLAccount"/>'s balance normally accumulates on
/// per Stage 02 <c>blocks-financial-schema-design.md</c> §3.1. Derived
/// from <see cref="GLAccountType"/>:
/// <list type="bullet">
///   <item><see cref="GLAccountType.Asset"/> → <see cref="Debit"/></item>
///   <item><see cref="GLAccountType.Expense"/> → <see cref="Debit"/></item>
///   <item><see cref="GLAccountType.Liability"/> → <see cref="Credit"/></item>
///   <item><see cref="GLAccountType.Equity"/> → <see cref="Credit"/></item>
///   <item><see cref="GLAccountType.Revenue"/> → <see cref="Credit"/></item>
/// </list>
/// </summary>
public enum NormalBalance
{
    Debit,
    Credit,
}

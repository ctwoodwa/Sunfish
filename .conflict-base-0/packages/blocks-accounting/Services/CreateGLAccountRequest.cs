using Sunfish.Blocks.Accounting.Models;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Input for <see cref="IAccountingService.CreateAccountAsync"/>.
/// </summary>
/// <param name="Code">
/// Account code (e.g. <c>"4000"</c>). Must be unique within the service instance.
/// </param>
/// <param name="Name">Display name of the account (e.g. <c>"Rental Revenue"</c>).</param>
/// <param name="Type">Accounting category (Asset, Liability, Equity, Revenue, Expense).</param>
/// <param name="ParentAccountId">
/// Optional parent for hierarchical chart-of-accounts structures.
/// The referenced account must already exist.
/// </param>
public sealed record CreateGLAccountRequest(
    string Code,
    string Name,
    GLAccountType Type,
    GLAccountId? ParentAccountId = null);

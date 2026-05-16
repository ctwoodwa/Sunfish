using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Seeds;

/// <summary>
/// One row in a <see cref="ChartTemplate"/>. Expands into a
/// <see cref="GLAccount"/> via
/// <see cref="Services.IChartSeedingService.SeedChartAsync"/>.
/// </summary>
/// <param name="Code">Account code (unique within the template).</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Accounting type — Asset / Liability / Equity / Revenue / Expense.</param>
/// <param name="Subtype">Stage 02 §3.1 sub-classification.</param>
/// <param name="ParentCode">
/// Optional reference (by <see cref="Code"/>) to the parent account in
/// the same template. Null for top-level group nodes.
/// </param>
/// <param name="IsPostable">
/// <c>false</c> for header / rollup parents that should not receive
/// journal-entry postings directly. Defaults to <c>true</c>.
/// </param>
public sealed record ChartTemplateAccount(
    string Code,
    string Name,
    GLAccountType Type,
    AccountSubtype Subtype,
    string? ParentCode = null,
    bool IsPostable = true);

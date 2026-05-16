namespace Sunfish.Blocks.FinancialLedger.Seeds;

/// <summary>
/// A reusable starter chart-of-accounts shape — a name, a description
/// for selection UI, and a list of <see cref="ChartTemplateAccount"/>
/// rows to expand into <see cref="Models.GLAccount"/> records when
/// seeding a fresh <see cref="Models.ChartOfAccounts"/>.
/// </summary>
/// <param name="Name">Human-readable template name (shown in the chart-picker UI).</param>
/// <param name="Description">One-paragraph description of the intended audience.</param>
/// <param name="Accounts">
/// The template's account list. Parents MUST appear before any child
/// that references them via <see cref="ChartTemplateAccount.ParentCode"/>
/// — the seeding service topologically sorts, but the lint-style
/// invariant is that authors can read the template top-to-bottom.
/// </param>
public sealed record ChartTemplate(
    string Name,
    string Description,
    IReadOnlyList<ChartTemplateAccount> Accounts);

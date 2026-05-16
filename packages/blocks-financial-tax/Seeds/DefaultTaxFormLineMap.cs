using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Seeds;

/// <summary>
/// First-pass IRS form line mappings, seeded against the
/// <c>blocks-financial-ledger</c> <c>DefaultChartTemplates.RentalRealEstate</c>
/// account codes.
///
/// <para>
/// <b>Provisional discipline.</b> Every row this class emits has
/// <see cref="TaxFormLineMap.IsProvisional"/> <c>= true</c> and a
/// <see cref="TaxFormLineMap.ProvisionalRationale"/> pointing at the
/// in-flight ONR (Office of Notarial Research) regulatory input file
/// at <c>icm/02_architecture/regulatory-us-rental-tax-input.md</c>.
/// When that input lands, a follow-on hand-off
/// (<c>blocks-financial-tax-onr-ratification-addendum-stage06-handoff.md</c>)
/// will (a) flip <see cref="TaxFormLineMap.IsProvisional"/> to
/// <c>false</c> on every row ONR confirms, (b) add/remove/edit any
/// rows ONR specifies, (c) bump <see cref="TaxFormLineMap.Version"/>
/// on every changed row, (d) emit
/// <c>Reports.TaxFormLineMapEdited</c> events.
/// </para>
/// </summary>
public static class DefaultTaxFormLineMap
{
    private const string ProvisionalReason =
        "Pending ONR ratification per regulatory-us-rental-tax-input.md (2026-05-16).";

    /// <summary>
    /// IRS Schedule E (Form 1040) line mapping, lines 3-22. Income
    /// (Lines 3-4) + expenses (Lines 5-19) reference exact account
    /// codes from <c>DefaultChartTemplates.RentalRealEstate</c>;
    /// totals + result (Lines 20-22) carry empty selector lists
    /// because they're computed by the Schedule E generator
    /// (downstream in <c>Sunfish.Blocks.Reports.Tax</c>).
    /// </summary>
    /// <param name="chartId">The chart these rows belong to.</param>
    /// <param name="taxYear">IRS form year. Defaults to 2026.</param>
    /// <param name="createdAtUtc">Timestamp for the freshly-stamped rows; defaults to now.</param>
    public static IReadOnlyList<TaxFormLineMap> ScheduleE(
        FL.ChartOfAccountsId chartId,
        int taxYear = 2026,
        Instant? createdAtUtc = null)
    {
        TaxFormLineMap Row(string line, string description, TaxAccountSelector[] selectors, string irsCitation, bool perProperty = true) =>
            TaxFormLineMap.Create(
                chartId: chartId,
                formKind: TaxFormKind.ScheduleE,
                taxYear: taxYear,
                line: line,
                description: description,
                selectors: selectors,
                perPropertyDimension: perProperty,
                isProvisional: true,
                provisionalRationale: ProvisionalReason,
                citationSource: $"IRS Pub 527 ({taxYear}); Schedule E (Form 1040) {taxYear}; {irsCitation}",
                createdAtUtc: createdAtUtc);

        return new[]
        {
            // ── Income lines (3-4) ─────────────────────────────────

            Row("Line3", "Rents received",
                new[] { new TaxAccountSelector(AccountCodePrefix: "41") },
                "Line 3 (Rents received)"),

            Row("Line4", "Royalties received",
                // No royalty account in the default chart; tag-only.
                new[] { new TaxAccountSelector(AccountTag: "royalty-income") },
                "Line 4 (Royalties received)"),

            // ── Expense lines (5-19) ───────────────────────────────

            Row("Line5", "Advertising",
                new[] { new TaxAccountSelector(AccountCode: "5100") },
                "Line 5 (Advertising)"),

            Row("Line6", "Auto and travel",
                // No account in default chart; tag-only.
                new[] { new TaxAccountSelector(AccountTag: "auto-travel") },
                "Line 6 (Auto and travel)"),

            Row("Line7", "Cleaning and maintenance",
                new[] { new TaxAccountSelector(AccountCode: "5200") },
                "Line 7 (Cleaning and maintenance)"),

            Row("Line8", "Commissions",
                new[] { new TaxAccountSelector(AccountTag: "commissions") },
                "Line 8 (Commissions)"),

            Row("Line9", "Insurance",
                new[] { new TaxAccountSelector(AccountCode: "5300") },
                "Line 9 (Insurance)"),

            Row("Line10", "Legal and other professional fees",
                new[] { new TaxAccountSelector(AccountCode: "5400") },
                "Line 10 (Legal and other professional fees)"),

            Row("Line11", "Management fees",
                new[] { new TaxAccountSelector(AccountCode: "5500") },
                "Line 11 (Management fees)"),

            Row("Line12", "Mortgage interest paid to banks",
                new[] { new TaxAccountSelector(AccountCode: "7110") },
                "Line 12 (Mortgage interest paid to banks)"),

            Row("Line13", "Other interest",
                new[] { new TaxAccountSelector(AccountTag: "other-interest") },
                "Line 13 (Other interest)"),

            Row("Line14", "Repairs",
                new[] { new TaxAccountSelector(AccountCode: "5600") },
                "Line 14 (Repairs)"),

            Row("Line15", "Supplies",
                new[] { new TaxAccountSelector(AccountCode: "5700") },
                "Line 15 (Supplies)"),

            // Property tax flows here per the hand-off's Halt 5 — property tax
            // is NOT a TaxCode; it's a recurring vendor bill via blocks-financial-ap
            // mapped to Schedule E Line 16 through chart code 6100.
            Row("Line16", "Taxes",
                new[] { new TaxAccountSelector(AccountCode: "6100") },
                "Line 16 (Taxes — primarily property tax)"),

            Row("Line17", "Utilities",
                new[] { new TaxAccountSelector(AccountCode: "5800") },
                "Line 17 (Utilities)"),

            Row("Line18", "Depreciation expense or depletion",
                new[] { new TaxAccountSelector(AccountCode: "7200") },
                "Line 18 (Depreciation expense or depletion)"),

            Row("Line19", "Other (with description)",
                new[] { new TaxAccountSelector(AccountTag: "schedule-e-line-19-other") },
                "Line 19 (Other)"),

            // ── Computed totals + result (20-22) ───────────────────
            // Empty selectors — the Schedule E generator computes these from
            // the prior lines. Mapping is retained for line-coverage completeness
            // and as the canonical place to ratify the description text.

            Row("Line20", "Total expenses",
                Array.Empty<TaxAccountSelector>(),
                "Line 20 (Total expenses — sum lines 5-19)"),

            Row("Line21", "Income or loss",
                Array.Empty<TaxAccountSelector>(),
                "Line 21 (Income or loss — Line 3 + Line 4 − Line 20)"),

            Row("Line22", "Deductible rental real estate loss after limitation",
                Array.Empty<TaxAccountSelector>(),
                "Line 22 (Deductible rental real estate loss after limitation)"),
        };
    }
}

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Tax kind per Stage 02 §3.12. Stable string codes per CRDT-conventions
/// §5 — member names match the canonical strings the persistence layer
/// writes (we use <c>enum.ToString()</c> for SQLite serialization,
/// never an int cast). Members are append-only — never renumber,
/// never remove.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property tax is intentionally absent</b> from this enum. Property
/// tax is modeled as a recurring vendor bill via <c>blocks-financial-ap</c>
/// + the chart-of-accounts code 6100 (Schedule E Line 16), not as a
/// <see cref="TaxCode"/>. See blocks-financial-tax-stage06-handoff Halt
/// condition 5.
/// </para>
/// </remarks>
public enum TaxKind
{
    /// <summary>US-style sales tax.</summary>
    Sales,

    /// <summary>Value-Added Tax (Europe etc.).</summary>
    VAT,

    /// <summary>Goods &amp; Services Tax (Canada, AU).</summary>
    GST,

    /// <summary>Tax withheld at source (e.g. payroll, contractor 1099 withholding).</summary>
    WithholdingTax,

    /// <summary>Self-assessed use tax — applied to in-bound goods where seller didn't collect.</summary>
    Use,

    /// <summary>Explicit zero rate — preserves audit trail vs. just omitting the code.</summary>
    Exempt,

    /// <summary>Anything not covered by the prior buckets.</summary>
    Other,
}

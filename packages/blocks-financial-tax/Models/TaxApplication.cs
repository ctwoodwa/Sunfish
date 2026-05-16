namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// How a <see cref="TaxCode"/> applies to a line amount per Stage 02
/// §3.12 + §6.4. Stable string codes per CRDT-conventions §5 — names
/// match the canonical serialization strings; members are append-only.
/// </summary>
public enum TaxApplication
{
    /// <summary>tax = subtotal × rate. Standard US sales-tax shape.</summary>
    OnSubtotal,

    /// <summary>tax = (subtotal + prior_tax) × rate. Tax-on-tax. The
    /// PR 3 calculation engine walks jurisdictions outermost-first
    /// (federal → state → county → city) per Stage 02 §6.4.</summary>
    Compound,

    /// <summary>Line price already includes tax — back out the tax
    /// portion: tax = subtotal − (subtotal ÷ (1 + rate)). Common in
    /// VAT regimes.</summary>
    Inclusive,
}

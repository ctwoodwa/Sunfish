namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Jurisdiction levels per Stage 02 §3.14. Stable string codes per
/// CRDT-conventions §5 — names match the canonical serialization
/// strings; members are append-only.
/// </summary>
/// <remarks>
/// Compound-tax application walks outermost-first per Stage 02 §6.4
/// (Country / Federal apply before State; State before County; etc.).
/// Use <see cref="JurisdictionLevelExtensions.OrderIndex"/> for that
/// ordering rather than relying on enum-declaration order — declaration
/// order is informational only, ordinals are explicit.
/// </remarks>
public enum JurisdictionLevel
{
    /// <summary>Sovereign country level (most senior).</summary>
    Country,

    /// <summary>Federal level — for federations like the US that distinguish federal from country.</summary>
    Federal,

    /// <summary>State / province / canton.</summary>
    State,

    /// <summary>County / borough / parish.</summary>
    County,

    /// <summary>Incorporated city / municipality.</summary>
    City,

    /// <summary>Sub-municipal special-purpose district (e.g. school district, transit district).</summary>
    District,

    /// <summary>Special / catch-all (e.g. tribal, port authority).</summary>
    Special,
}

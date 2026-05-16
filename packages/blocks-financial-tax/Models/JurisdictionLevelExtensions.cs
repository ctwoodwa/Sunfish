namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Ordering helper for compound-tax application per Stage 02 §6.4 +
/// blocks-financial-tax-stage06-handoff "Important context" #3.
/// </summary>
public static class JurisdictionLevelExtensions
{
    /// <summary>
    /// Ordering for compound-tax application — outermost jurisdiction
    /// first. Country/Federal apply before State; State before County;
    /// County before City; etc.
    ///
    /// <para>
    /// Stable across enum-member additions per CRDT-conventions §5:
    /// when a new <see cref="JurisdictionLevel"/> member is added, give
    /// it an explicit ordinal here rather than relying on enum-
    /// declaration order. Unknown values return <c>99</c> (sort last)
    /// for forward-compat.
    /// </para>
    /// </summary>
    public static int OrderIndex(this JurisdictionLevel level) => level switch
    {
        JurisdictionLevel.Country  => 0,
        JurisdictionLevel.Federal  => 1,
        JurisdictionLevel.State    => 2,
        JurisdictionLevel.County   => 3,
        JurisdictionLevel.City     => 4,
        JurisdictionLevel.District => 5,
        JurisdictionLevel.Special  => 6,
        _                          => 99,
    };
}

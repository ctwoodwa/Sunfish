using Sunfish.Blocks.FinancialTax.Services;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Query-based <c>rates</c> accessor for <see cref="TaxCode"/>,
/// preserving the Stage 02 §3.12 surface without embedding the
/// sub-collection on the entity (which would violate CRDT §4
/// "Append-only sub-collections — never as an embedded array").
/// </summary>
public static class TaxCodeRatesExtensions
{
    /// <summary>
    /// Returns every <see cref="TaxRate"/> associated with this
    /// <see cref="TaxCode"/> across all jurisdictions, ordered by
    /// (JurisdictionId, EffectiveDate). Equivalent to the Stage 02
    /// §3.12 <c>rates</c> accessor; query-based rather than embedded.
    /// </summary>
    public static Task<IReadOnlyList<TaxRate>> GetRatesAsync(
        this TaxCode code,
        ITaxRateLookup lookup,
        CancellationToken cancellationToken = default)
    {
        if (code is null) throw new ArgumentNullException(nameof(code));
        if (lookup is null) throw new ArgumentNullException(nameof(lookup));
        return lookup.GetAllForTaxCodeAsync(code.Id, cancellationToken);
    }
}

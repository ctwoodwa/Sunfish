using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// In-memory implementation of <see cref="ITaxJurisdictionResolver"/>
/// that walks the jurisdiction tree by matching <c>IsoCountry</c> +
/// <c>Region</c> + <c>Locality</c> against the store. PR 1 ships this
/// path only; ZIP-code resolution and per-property look-aside join
/// land in later hand-offs.
///
/// <para>
/// Algorithm: enumerate jurisdictions by level in
/// <see cref="JurisdictionLevelExtensions.OrderIndex"/> order (so we
/// can prune impossible matches early), keep the rows whose address
/// columns match the context's address columns at the same granularity,
/// and return them ordered most-local-first
/// (<see cref="JurisdictionLevel.City"/> → <see cref="JurisdictionLevel.County"/>
/// → <see cref="JurisdictionLevel.State"/> → <see cref="JurisdictionLevel.Federal"/>
/// → <see cref="JurisdictionLevel.Country"/>).
/// </para>
/// </summary>
public sealed class InMemoryTaxJurisdictionResolver : ITaxJurisdictionResolver
{
    private readonly ITaxJurisdictionStore _store;

    public InMemoryTaxJurisdictionResolver(ITaxJurisdictionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaxJurisdiction>> ResolveAsync(
        TaxLocationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var matches = new List<TaxJurisdiction>();

        foreach (var level in (JurisdictionLevel[])Enum.GetValues(typeof(JurisdictionLevel)))
        {
            var rows = await _store.GetByLevelAsync(level, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (!row.IsActive) continue;
                if (!CountryMatches(row, context)) continue;
                if (!RegionMatches(row, context, level)) continue;
                if (!LocalityMatches(row, context, level)) continue;
                matches.Add(row);
            }
        }

        // Return most-local-first per the contract — sort descending by
        // OrderIndex (City=4 comes before State=2 comes before Country=0).
        // Compound-tax callers re-sort via OrderIndex ascending.
        return matches
            .OrderByDescending(j => j.Level.OrderIndex())
            .ToList();
    }

    private static bool CountryMatches(TaxJurisdiction row, TaxLocationContext context) =>
        string.Equals(row.IsoCountry, context.IsoCountry, StringComparison.OrdinalIgnoreCase);

    private static bool RegionMatches(TaxJurisdiction row, TaxLocationContext context, JurisdictionLevel level)
    {
        // Country / Federal rows don't carry a region; they match every
        // address inside their country.
        if (level == JurisdictionLevel.Country || level == JurisdictionLevel.Federal)
        {
            return true;
        }
        if (row.Region is null) return false;
        if (context.Region is null) return false;
        return string.Equals(row.Region, context.Region, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LocalityMatches(TaxJurisdiction row, TaxLocationContext context, JurisdictionLevel level)
    {
        // Country / Federal / State rows don't filter by locality.
        if (level == JurisdictionLevel.Country
            || level == JurisdictionLevel.Federal
            || level == JurisdictionLevel.State)
        {
            return true;
        }
        if (row.Locality is null) return false;
        if (context.Locality is null) return false;
        return string.Equals(row.Locality, context.Locality, StringComparison.OrdinalIgnoreCase);
    }
}

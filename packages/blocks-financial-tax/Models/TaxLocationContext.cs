namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Input to <c>ITaxJurisdictionResolver.ResolveAsync</c> — the location
/// or property whose applicable jurisdictions we want to walk.
///
/// <para>
/// When <see cref="PropertyId"/> is set it takes precedence over the
/// address fields — the resolver looks up the property's stored
/// jurisdiction chain rather than re-deriving it from the address. The
/// address fields remain the fallback (and the only path for cash-sale
/// transactions where no property is involved).
/// </para>
/// </summary>
/// <param name="IsoCountry">ISO 3166-1 alpha-2 country code. Required.</param>
/// <param name="Region">ISO 3166-2 region code (e.g. <c>"US-VA"</c>).</param>
/// <param name="Locality">Locality string (e.g. <c>"Frederick County"</c>, <c>"Winchester"</c>).</param>
/// <param name="PostalCode">Not used in PR 1; reserved for ZIP-code lookup in a future hand-off.</param>
/// <param name="PropertyId">When set, takes precedence over the address fields. Opaque string FK.</param>
public sealed record TaxLocationContext(
    string IsoCountry,
    string? Region = null,
    string? Locality = null,
    string? PostalCode = null,
    string? PropertyId = null);

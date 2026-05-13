namespace Sunfish.Blocks.Quarterdeck;

/// <summary>
/// A single search result surfaced by the Quarterdeck <see cref="SearchPanel"/>.
/// Hosts provide an <see cref="Sunfish.UICore.Primitives.ISearchAsYouType{T}"/>
/// implementation backed by whatever index is appropriate (e.g., Wayfinder paths,
/// Standing Order titles, Department display names).
/// </summary>
/// <param name="StableKey">
/// Stable, unique key for this result — used as the suffix of the ARIA
/// <c>aria-activedescendant</c> id (<c>quarterdeck-search-result-{StableKey}</c>).
/// Must be non-empty and safe to use in an HTML id attribute (no whitespace).
/// </param>
/// <param name="Label">Human-readable display label rendered in the result list.</param>
/// <param name="TargetHref">
/// Optional navigation target; null when the caller handles navigation via
/// the <see cref="SearchPanel.OnResultSelected"/> callback instead.
/// </param>
public sealed record QuarterdeckSearchResult(
    string StableKey,
    string Label,
    string? TargetHref = null);

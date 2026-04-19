namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Body for a state personal-property tax return.
/// </summary>
/// <remarks>
/// Per-jurisdiction templates are deferred. This type acts as a schema carrier only —
/// the <see cref="Items"/> collection holds the raw asset inventory.
/// State-specific form layout, line mapping, and submission logic are future work.
/// </remarks>
/// <param name="StateCode">
/// Two-letter US state abbreviation (e.g. <c>"WA"</c>, <c>"TX"</c>).
/// </param>
/// <param name="Items">
/// Personal-property assets to be reported on this state return.
/// </param>
public sealed record StatePersonalPropertyBody(
    string StateCode,
    IReadOnlyList<PersonalPropertyRow> Items) : TaxReportBody
{
    /// <inheritdoc />
    public override TaxReportKind Kind => TaxReportKind.StatePersonalProperty;
}

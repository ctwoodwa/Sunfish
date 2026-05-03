namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// A single line item on a state personal-property tax return.
/// Per-state templates are deferred; this record carries the schema only.
/// </summary>
/// <param name="Description">Description of the personal-property asset.</param>
/// <param name="AcquisitionYear">Year the asset was acquired.</param>
/// <param name="OriginalCost">Original cost basis of the asset.</param>
/// <param name="ReportedValue">Assessed or reported value for the tax period.</param>
public sealed record PersonalPropertyRow(
    string Description,
    int AcquisitionYear,
    decimal OriginalCost,
    decimal ReportedValue);

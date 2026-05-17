namespace Sunfish.Blocks.WorkProjects.Migration;

/// <summary>
/// Source record from an ERPNext <c>Project</c> doctype. Mirrors the
/// Frappe API field shape so the importer can be fed directly from
/// <c>erpnext.ts</c> or a Frappe HTTP client. <see cref="Modified"/> is
/// the ERPNext version key — when unchanged the importer returns
/// <see cref="ImportOutcomeKind.Skipped"/>.
/// </summary>
public sealed record ErpnextProjectSource(
    string Name,
    string Modified,
    string ProjectName,
    string Status,
    DateOnly? ExpectedStartDate,
    DateOnly? ExpectedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    string? Customer,
    string? CostCenter,
    decimal? EstimatedCosting,
    string? ProjectType = null);

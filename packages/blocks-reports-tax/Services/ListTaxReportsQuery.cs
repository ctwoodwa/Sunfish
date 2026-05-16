using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Optional filter parameters for <see cref="ITaxReportingService.ListAsync"/>.
/// All filters are additive (AND). Omit a field to skip that filter.
/// </summary>
/// <param name="Year">Filter by tax year.</param>
/// <param name="Kind">Filter by report kind.</param>
/// <param name="Status">Filter by report status.</param>
/// <param name="PropertyId">Filter by property scope.</param>
public sealed record ListTaxReportsQuery(
    TaxYear? Year = null,
    TaxReportKind? Kind = null,
    TaxReportStatus? Status = null,
    EntityId? PropertyId = null);

using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Input model for generating a Schedule E tax report via
/// <see cref="ITaxReportingService.GenerateScheduleEAsync"/>.
/// </summary>
/// <param name="Year">The tax year this Schedule E covers.</param>
/// <param name="Properties">
/// Per-property income and expense rows. The service computes aggregate totals from these rows.
/// </param>
/// <param name="PropertyId">
/// Optional: set to a single property's <see cref="EntityId"/> for a per-property report.
/// <see langword="null"/> for an aggregate report spanning all properties.
/// </param>
public sealed record ScheduleEGenerationRequest(
    TaxYear Year,
    IReadOnlyList<SchedulePropertyRow> Properties,
    EntityId? PropertyId = null);

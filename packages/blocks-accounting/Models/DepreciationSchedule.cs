using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Accounting.Models;

/// <summary>
/// Passive record capturing the configuration of a fixed-asset depreciation schedule.
/// </summary>
/// <remarks>
/// <b>This pass: shape only — no computation.</b>
/// Schedule-line generation (producing periodic depreciation journal entries) is deferred
/// to the G17 follow-up pass, once the event bus and automatic JE generation are in place.
/// See TODO below.
/// </remarks>
/// <param name="Id">Unique depreciation schedule identifier.</param>
/// <param name="AssetId">
/// Reference to the fixed asset being depreciated, expressed as a Sunfish <see cref="EntityId"/>
/// (e.g. <c>asset:acme-realty/building-42</c>).
/// </param>
/// <param name="StartDate">First day of the first depreciation period.</param>
/// <param name="OriginalCost">Total acquisition cost of the asset. Must be non-negative.</param>
/// <param name="SalvageValue">
/// Estimated residual value at end of useful life. Must be non-negative and ≤ <paramref name="originalCost"/>.
/// </param>
/// <param name="UsefulLifeMonths">
/// Expected useful life in months. Must be a positive integer.
/// </param>
/// <param name="Method">Cost-allocation method (straight-line, declining-balance, units-of-production).</param>
// TODO (G17 follow-up): Implement ComputeScheduleAsync on IAccountingService to generate
// a List<DepreciationEntry> (period, amount, cumulativeDepreciation, bookValue) from this record.
// This pass intentionally omits the computation to keep the scope of G17 first-pass minimal.
public sealed record DepreciationSchedule(
    DepreciationScheduleId Id,
    EntityId AssetId,
    DateOnly StartDate,
    decimal OriginalCost,
    decimal SalvageValue,
    int UsefulLifeMonths,
    DepreciationMethod Method);

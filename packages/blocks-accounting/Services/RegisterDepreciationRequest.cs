using Sunfish.Blocks.Accounting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Accounting.Services;

/// <summary>
/// Input for <see cref="IAccountingService.RegisterScheduleAsync"/>.
/// </summary>
/// <param name="AssetId">
/// Reference to the fixed asset, expressed as a Sunfish <see cref="EntityId"/>
/// (e.g. <c>asset:acme-realty/building-42</c>).
/// </param>
/// <param name="StartDate">First day of the first depreciation period.</param>
/// <param name="OriginalCost">Total acquisition cost. Must be non-negative.</param>
/// <param name="SalvageValue">
/// Estimated residual value at end of useful life. Must be &lt;= <paramref name="OriginalCost"/>.
/// </param>
/// <param name="UsefulLifeMonths">Expected useful life in months. Must be positive.</param>
/// <param name="Method">Cost-allocation method.</param>
public sealed record RegisterDepreciationRequest(
    EntityId AssetId,
    DateOnly StartDate,
    decimal OriginalCost,
    decimal SalvageValue,
    int UsefulLifeMonths,
    DepreciationMethod Method);

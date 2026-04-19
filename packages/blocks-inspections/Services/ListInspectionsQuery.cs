using Sunfish.Blocks.Inspections.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// Optional filter parameters for <see cref="IInspectionsService.ListInspectionsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListInspectionsQuery
{
    /// <summary>When set, only inspections for this unit are returned.</summary>
    public EntityId? UnitId { get; init; }

    /// <summary>When set, only inspections in this phase are returned.</summary>
    public InspectionPhase? Phase { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListInspectionsQuery Empty { get; } = new();
}

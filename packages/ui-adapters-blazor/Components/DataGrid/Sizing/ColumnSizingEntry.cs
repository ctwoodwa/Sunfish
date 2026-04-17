using Sunfish.Components.Blazor.Components.DataGrid;

namespace Sunfish.Components.Blazor.Components.DataGrid.Sizing;

/// <summary>
/// Input model for a single column width resolution pass.
/// </summary>
public sealed record ColumnSizingEntry(
    string Id,
    string? ExplicitWidth,
    double MinWidth,
    double? MaxWidth,
    string? TextAlign = null,
    bool Locked = false,
    GridColumnFrozenPosition FrozenPosition = GridColumnFrozenPosition.Start);

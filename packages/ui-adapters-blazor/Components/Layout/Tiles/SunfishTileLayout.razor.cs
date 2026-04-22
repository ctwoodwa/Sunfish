using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Layout.Tiles;

/// <summary>
/// Canonical MVP tile-layout surface for Sunfish Blazor. Arranges child
/// <see cref="SunfishTile"/> elements on a CSS grid with configurable column
/// count, row height, and gap. Drag-to-reorder and drag-to-resize are
/// deferred (see component-mapping.json: status "partial").
/// </summary>
public partial class SunfishTileLayout : SunfishComponentBase
{
    /// <summary>Number of CSS grid columns. Defaults to <c>4</c>.</summary>
    [Parameter] public int Columns { get; set; } = 4;

    /// <summary>Row height in pixels. Defaults to <c>140</c>.</summary>
    [Parameter] public int RowHeight { get; set; } = 140;

    /// <summary>Gap between tiles in pixels. Defaults to <c>12</c>.</summary>
    [Parameter] public int Gap { get; set; } = 12;

    /// <summary>
    /// Resize behaviour. MVP accepts the value but does not render resize
    /// handles yet; tracked as a documented gap.
    /// </summary>
    [Parameter] public TileLayoutResizing Resizable { get; set; } = TileLayoutResizing.None;

    /// <summary>
    /// Whether tiles can be reordered by drag-and-drop. MVP stores the flag
    /// but does not wire drag handlers yet; tracked as a documented gap.
    /// </summary>
    [Parameter] public bool Reorderable { get; set; }

    /// <summary>Cancellable callback fired when a tile is resized.</summary>
    [Parameter] public EventCallback<TileResizeEventArgs> OnTileResize { get; set; }

    /// <summary>Cancellable callback fired when a tile is reordered.</summary>
    [Parameter] public EventCallback<TileReorderCancellableEventArgs> OnTileReorder { get; set; }

    /// <summary>Nested tile declarations.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private readonly List<SunfishTile> _tiles = new();

    internal void RegisterTile(SunfishTile tile)
    {
        if (!_tiles.Contains(tile))
        {
            _tiles.Add(tile);
        }
    }

    internal void UnregisterTile(SunfishTile tile) => _tiles.Remove(tile);

    /// <summary>
    /// Public reorder entry point used by future drag-and-drop wiring. Fires
    /// <see cref="OnTileReorder"/> with cancellation support and returns
    /// <c>true</c> when the consumer allows the reorder.
    /// </summary>
    public async Task<bool> RequestReorderAsync(string tileId, int oldIndex, int newIndex)
    {
        var args = new TileReorderCancellableEventArgs
        {
            TileId = tileId,
            OldIndex = oldIndex,
            NewIndex = newIndex,
        };
        await OnTileReorder.InvokeAsync(args);
        return !args.IsCancelled;
    }

    /// <summary>
    /// Public resize entry point used by future drag-and-drop wiring. Fires
    /// <see cref="OnTileResize"/> with cancellation support and returns
    /// <c>true</c> when the consumer allows the resize.
    /// </summary>
    public async Task<bool> RequestResizeAsync(string tileId, int newColSpan, int newRowSpan)
    {
        var args = new TileResizeEventArgs
        {
            TileId = tileId,
            NewColSpan = newColSpan,
            NewRowSpan = newRowSpan,
        };
        await OnTileResize.InvokeAsync(args);
        return !args.IsCancelled;
    }

    private string GridStyle()
    {
        var cols = Columns > 0 ? Columns : 1;
        var rh = RowHeight > 0 ? RowHeight : 140;
        var gap = Gap >= 0 ? Gap : 0;
        return string.Format(
            CultureInfo.InvariantCulture,
            "display:grid;grid-template-columns:repeat({0},1fr);grid-auto-rows:{1}px;gap:{2}px;",
            cols, rh, gap);
    }
}

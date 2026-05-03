namespace Sunfish.UIAdapters.Blazor.Components.Layout.Tiles;

/// <summary>
/// Cancellable event arguments fired when a <c>SunfishTile</c> is resized
/// inside a <c>SunfishTileLayout</c>. Handlers may set <see cref="IsCancelled"/>
/// to veto the resize.
/// </summary>
public class TileResizeEventArgs
{
    /// <summary>Identifier of the tile being resized.</summary>
    public string TileId { get; set; } = string.Empty;

    /// <summary>Proposed new column span for the tile.</summary>
    public int NewColSpan { get; set; }

    /// <summary>Proposed new row span for the tile.</summary>
    public int NewRowSpan { get; set; }

    /// <summary>Set to <c>true</c> to cancel the resize operation.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Cancellable event arguments fired when a <c>SunfishTile</c> is reordered
/// inside a <c>SunfishTileLayout</c>. Handlers may set <see cref="IsCancelled"/>
/// to veto the reorder.
/// </summary>
public class TileReorderCancellableEventArgs
{
    /// <summary>Identifier of the tile being moved.</summary>
    public string TileId { get; set; } = string.Empty;

    /// <summary>Zero-based index of the tile before the reorder.</summary>
    public int OldIndex { get; set; }

    /// <summary>Zero-based index of the tile after the reorder.</summary>
    public int NewIndex { get; set; }

    /// <summary>Set to <c>true</c> to cancel the reorder operation.</summary>
    public bool IsCancelled { get; set; }
}

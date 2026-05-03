namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a tile item in the SunfishTileLayout.
/// </summary>
public class TileLayoutItem
{
    public string? Title { get; set; }

    /// <summary>
    /// Opaque content payload. Adapter packages resolve this to their
    /// platform-specific render type (e.g. RenderFragment in Blazor).
    /// </summary>
    public object? Content { get; set; }

    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public int? Order { get; set; }
}

/// <summary>
/// Event args for tile reorder operations.
/// </summary>
public class TileReorderEventArgs
{
    public TileLayoutItem Item { get; set; } = default!;
    public int OldIndex { get; set; }
    public int NewIndex { get; set; }
}

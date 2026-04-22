namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Resize behaviour for tiles inside a <c>SunfishTileLayout</c>. The MVP
/// surface accepts the value but does not yet render drag handles — tracked
/// as a documented gap until the drag/resize interactions land.
/// </summary>
public enum TileLayoutResizing
{
    /// <summary>Tiles are fixed-size and cannot be resized by the user.</summary>
    None,

    /// <summary>Tiles can be resized by the user (interaction deferred).</summary>
    Enabled,
}

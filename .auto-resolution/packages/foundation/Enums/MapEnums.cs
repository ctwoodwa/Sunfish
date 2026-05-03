namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the type of a map layer.
/// </summary>
public enum MapLayerType
{
    /// <summary>A tile layer rendering raster or vector base-map tiles.</summary>
    Tile,

    /// <summary>A marker layer displaying point-of-interest markers.</summary>
    Marker,

    /// <summary>A shape layer rendering GeoJSON polygons and lines.</summary>
    Shape,

    /// <summary>A bubble layer rendering sized circles at geographic coordinates.</summary>
    Bubble
}

/// <summary>
/// Specifies the visual shape of a map marker.
/// </summary>
public enum MapMarkerShape
{
    /// <summary>A standard map pin.</summary>
    Pin,

    /// <summary>A map pin with a targeting crosshair.</summary>
    PinTarget
}

/// <summary>
/// Specifies the position of map navigation controls.
/// </summary>
public enum MapControlsPosition
{
    /// <summary>Controls are placed in the top-left corner.</summary>
    TopLeft,

    /// <summary>Controls are placed in the top-right corner.</summary>
    TopRight,

    /// <summary>Controls are placed in the bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Controls are placed in the bottom-right corner.</summary>
    BottomRight
}

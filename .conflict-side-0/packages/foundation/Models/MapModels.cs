namespace Sunfish.Foundation.Models;

/// <summary>A geographic location with latitude and longitude.</summary>
public class MapLocation
{
    /// <summary>Latitude coordinate (-90 to 90).</summary>
    public double Latitude { get; set; }
    /// <summary>Longitude coordinate (-180 to 180).</summary>
    public double Longitude { get; set; }
}

/// <summary>Event arguments for a map surface click.</summary>
public class MapClickEventArgs : EventArgs
{
    /// <summary>Geographic location of the click.</summary>
    public MapLocation? Location { get; set; }
}

/// <summary>Event arguments for a marker click on the map.</summary>
public class MapMarkerClickEventArgs : EventArgs
{
    /// <summary>The marker data item that was clicked.</summary>
    public MapMarker? DataItem { get; set; }
}

/// <summary>Event arguments for a shape or bubble click on the map.</summary>
public class MapShapeClickEventArgs : EventArgs
{
    /// <summary>Index of the shape within its layer.</summary>
    public int ShapeIndex { get; set; }
}

/// <summary>Event arguments for a zoom end event.</summary>
public class MapZoomEndEventArgs : EventArgs
{
    /// <summary>New zoom level after the operation.</summary>
    public double Zoom { get; set; }
    /// <summary>Map center after the zoom.</summary>
    public MapCenter? Center { get; set; }
}

/// <summary>Event arguments for a pan end event.</summary>
public class MapPanEndEventArgs : EventArgs
{
    /// <summary>Map center after the pan.</summary>
    public MapCenter? Center { get; set; }
}

/// <summary>
/// A marker placed on a <c>SunfishMap</c> at a geographic coordinate.
/// </summary>
public class MapMarker
{
    /// <summary>Tooltip or label for the marker.</summary>
    public string? Title { get; set; }

    /// <summary>Latitude coordinate (-90 to 90).</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude coordinate (-180 to 180).</summary>
    public double Longitude { get; set; }
}

/// <summary>
/// Geographic center point for a <c>SunfishMap</c>.
/// </summary>
public class MapCenter
{
    /// <summary>Latitude coordinate (-90 to 90).</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude coordinate (-180 to 180).</summary>
    public double Longitude { get; set; }
}

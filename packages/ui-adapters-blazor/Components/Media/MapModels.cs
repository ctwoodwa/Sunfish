using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Media;

/// <summary>
/// A marker placed on a <see cref="SunfishMap"/> at a geographic coordinate.
/// This is the Media-category MVP marker model, distinct from the richer
/// <c>Sunfish.Foundation.Models.MapMarker</c> used by the DataDisplay map adapter.
/// </summary>
public class MapMarker
{
    /// <summary>Latitude (-90 to 90).</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude (-180 to 180).</summary>
    public double Longitude { get; set; }

    /// <summary>Tooltip or popup label displayed when the marker is activated.</summary>
    public string? Title { get; set; }

    /// <summary>Optional popup body shown when the marker is clicked.</summary>
    public string? Popup { get; set; }

    /// <summary>Marker icon style.</summary>
    public MapMarkerIcon Icon { get; set; } = MapMarkerIcon.Default;

    /// <summary>Per-marker click callback.</summary>
    public EventCallback<MapMarker> OnClick { get; set; }
}

/// <summary>
/// Event payload for <see cref="SunfishMap.OnMapClick"/>.
/// </summary>
public class MapClickEventArgs
{
    /// <summary>Latitude of the click.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the click.</summary>
    public double Longitude { get; set; }
}

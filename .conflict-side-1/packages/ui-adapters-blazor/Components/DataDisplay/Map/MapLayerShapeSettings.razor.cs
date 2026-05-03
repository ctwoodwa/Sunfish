using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Map;

public partial class MapLayerShapeSettings : SunfishComponentBase
{
    /// <summary>The default fill color for shapes. Accepts a valid CSS color string.</summary>
    [Parameter] public string? FillColor { get; set; }

    /// <summary>The fill opacity (0 to 1) for shapes.</summary>
    [Parameter] public double? FillOpacity { get; set; }

    /// <summary>The stroke color for shapes. Accepts a valid CSS color string.</summary>
    [Parameter] public string? StrokeColor { get; set; }

    /// <summary>The default stroke width for shapes.</summary>
    [Parameter] public double? StrokeWidth { get; set; }

    [CascadingParameter] internal IMapLayerSettingsHost? ParentLayer { get; set; }

    protected override void OnInitialized()
    {
        ParentLayer?.RegisterShapeSettings(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentLayer?.UnregisterShapeSettings(this);
        base.Dispose(disposing);
    }
}

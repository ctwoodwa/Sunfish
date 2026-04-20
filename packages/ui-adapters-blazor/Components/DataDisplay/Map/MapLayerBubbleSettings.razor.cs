using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Map;

public partial class MapLayerBubbleSettings : SunfishComponentBase
{
    /// <summary>The default fill color for bubble symbols. Accepts a valid CSS color string.</summary>
    [Parameter] public string? FillColor { get; set; }

    /// <summary>The default fill opacity (0 to 1) for bubble symbols.</summary>
    [Parameter] public double? FillOpacity { get; set; }

    /// <summary>The default stroke color for bubble symbols. Accepts a valid CSS color string.</summary>
    [Parameter] public string? StrokeColor { get; set; }

    /// <summary>The default stroke width for bubble symbols.</summary>
    [Parameter] public double? StrokeWidth { get; set; }

    /// <summary>The minimum symbol size for bubble layer symbols.</summary>
    [Parameter] public double? MinSize { get; set; }

    /// <summary>The maximum symbol size for bubble layer symbols.</summary>
    [Parameter] public double? MaxSize { get; set; }

    [CascadingParameter] internal IMapLayerSettingsHost? ParentLayer { get; set; }

    protected override void OnInitialized()
    {
        ParentLayer?.RegisterBubbleSettings(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentLayer?.UnregisterBubbleSettings(this);
        base.Dispose(disposing);
    }
}

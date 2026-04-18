using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.DataDisplay.Map;

public partial class MapLayerMarkerSettings : SunfishComponentBase
{
    /// <summary>The JS function name used as a marker template.</summary>
    [Parameter] public string? Template { get; set; }

    [CascadingParameter] internal IMapLayerSettingsHost? ParentLayer { get; set; }

    protected override void OnInitialized()
    {
        ParentLayer?.RegisterMarkerSettings(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentLayer?.UnregisterMarkerSettings(this);
        base.Dispose(disposing);
    }
}

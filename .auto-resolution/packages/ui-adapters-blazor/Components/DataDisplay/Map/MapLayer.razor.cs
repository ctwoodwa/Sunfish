using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Map;

public partial class MapLayer : SunfishComponentBase, IMapLayerSettingsHost
{
    /// <summary>The type of map layer (Tile, Marker, Shape, Bubble).</summary>
    [Parameter] public MapLayerType Type { get; set; }

    /// <summary>URL template for tile layers. Supports {s}, {z}, {x}, {y} placeholders.</summary>
    [Parameter] public string? UrlTemplate { get; set; }

    /// <summary>Subdomain values to substitute for {s} in the URL template.</summary>
    [Parameter] public string[]? Subdomains { get; set; }

    /// <summary>Attribution text displayed on the map for this layer.</summary>
    [Parameter] public string? Attribution { get; set; }

    /// <summary>Data source for marker, shape, or bubble layers. For Shape layers, a GeoJSON string.</summary>
    [Parameter] public object? Data { get; set; }

    /// <summary>Property name on Data items containing the geographic location (for Marker/Bubble layers).</summary>
    [Parameter] public string? LocationField { get; set; }

    /// <summary>Property name on Data items containing the display title (for Marker layers).</summary>
    [Parameter] public string? TitleField { get; set; }

    /// <summary>Property name on Data items containing the numeric value (for Bubble layers).</summary>
    [Parameter] public string? ValueField { get; set; }

    /// <summary>Layer opacity (0.0 to 1.0). Defaults to 1.0.</summary>
    [Parameter] public double Opacity { get; set; } = 1.0;

    /// <summary>Minimum zoom level at which this layer is visible. Defaults to 0.</summary>
    [Parameter] public double MinZoom { get; set; } = 0;

    /// <summary>Maximum zoom level at which this layer is visible. Defaults to 22.</summary>
    [Parameter] public double MaxZoom { get; set; } = 22;

    /// <summary>Unique identifier for this layer. Auto-generated if not set.</summary>
    [Parameter] public string? LayerId { get; set; }

    /// <summary>Child content for settings components (e.g. MapLayerMarkerSettings, MapLayerBubbleSettings, MapLayerShapeSettings).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [CascadingParameter] internal IMapLayerHost? ParentMap { get; set; }

    internal string EffectiveLayerId => LayerId ?? _autoId;
    private readonly string _autoId = $"mar-layer-{Guid.NewGuid():N}";

    private MapLayerMarkerSettings? _markerSettings;
    private MapLayerBubbleSettings? _bubbleSettings;
    private MapLayerShapeSettings? _shapeSettings;

    /// <summary>The registered marker settings child component, if any.</summary>
    internal MapLayerMarkerSettings? MarkerSettings => _markerSettings;

    /// <summary>The registered bubble settings child component, if any.</summary>
    internal MapLayerBubbleSettings? BubbleSettings => _bubbleSettings;

    /// <summary>The registered shape settings child component, if any.</summary>
    internal MapLayerShapeSettings? ShapeSettings => _shapeSettings;

    void IMapLayerSettingsHost.RegisterMarkerSettings(MapLayerMarkerSettings settings)
    {
        _markerSettings = settings;
    }

    void IMapLayerSettingsHost.UnregisterMarkerSettings(MapLayerMarkerSettings settings)
    {
        if (ReferenceEquals(_markerSettings, settings))
            _markerSettings = null;
    }

    void IMapLayerSettingsHost.RegisterBubbleSettings(MapLayerBubbleSettings settings)
    {
        _bubbleSettings = settings;
    }

    void IMapLayerSettingsHost.UnregisterBubbleSettings(MapLayerBubbleSettings settings)
    {
        if (ReferenceEquals(_bubbleSettings, settings))
            _bubbleSettings = null;
    }

    void IMapLayerSettingsHost.RegisterShapeSettings(MapLayerShapeSettings settings)
    {
        _shapeSettings = settings;
    }

    void IMapLayerSettingsHost.UnregisterShapeSettings(MapLayerShapeSettings settings)
    {
        if (ReferenceEquals(_shapeSettings, settings))
            _shapeSettings = null;
    }

    /// <summary>
    /// Creates a <see cref="MapLayerDescriptor"/> from the current parameter values.
    /// </summary>
    internal MapLayerDescriptor ToDescriptor()
    {
        string? geoJsonData = null;
        if (Type == MapLayerType.Shape && Data is string geoJson)
        {
            geoJsonData = geoJson;
        }

        MapLayerStyleDescriptor? style = null;

        if (_bubbleSettings is not null)
        {
            style = new MapLayerStyleDescriptor(
                FillColor: _bubbleSettings.FillColor,
                FillOpacity: _bubbleSettings.FillOpacity,
                StrokeColor: _bubbleSettings.StrokeColor,
                StrokeWidth: _bubbleSettings.StrokeWidth,
                MinSize: _bubbleSettings.MinSize,
                MaxSize: _bubbleSettings.MaxSize);
        }
        else if (_shapeSettings is not null)
        {
            style = new MapLayerStyleDescriptor(
                FillColor: _shapeSettings.FillColor,
                FillOpacity: _shapeSettings.FillOpacity,
                StrokeColor: _shapeSettings.StrokeColor,
                StrokeWidth: _shapeSettings.StrokeWidth,
                MinSize: null,
                MaxSize: null);
        }

        return new MapLayerDescriptor(
            Id: EffectiveLayerId,
            Type: Type,
            UrlTemplate: UrlTemplate,
            Subdomains: Subdomains,
            Attribution: Attribution,
            GeoJsonData: geoJsonData,
            Data: (Type is MapLayerType.Marker or MapLayerType.Bubble) ? Data : null,
            LocationField: LocationField,
            TitleField: TitleField,
            ValueField: ValueField,
            Opacity: Opacity,
            MinZoom: MinZoom,
            MaxZoom: MaxZoom,
            Style: style);
    }

    protected override void OnInitialized()
    {
        ParentMap?.RegisterLayer(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ParentMap?.UnregisterLayer(this);
        base.Dispose(disposing);
    }
}

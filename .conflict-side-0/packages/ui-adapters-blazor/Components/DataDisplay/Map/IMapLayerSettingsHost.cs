namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Map;

/// <summary>
/// Interface for MapLayer components that accept child settings registrations via CascadingValue.
/// Implemented by <see cref="MapLayer"/>.
/// </summary>
internal interface IMapLayerSettingsHost
{
    /// <summary>Registers a marker settings child component.</summary>
    void RegisterMarkerSettings(MapLayerMarkerSettings settings);

    /// <summary>Unregisters a marker settings child component.</summary>
    void UnregisterMarkerSettings(MapLayerMarkerSettings settings);

    /// <summary>Registers a bubble settings child component.</summary>
    void RegisterBubbleSettings(MapLayerBubbleSettings settings);

    /// <summary>Unregisters a bubble settings child component.</summary>
    void UnregisterBubbleSettings(MapLayerBubbleSettings settings);

    /// <summary>Registers a shape settings child component.</summary>
    void RegisterShapeSettings(MapLayerShapeSettings settings);

    /// <summary>Unregisters a shape settings child component.</summary>
    void UnregisterShapeSettings(MapLayerShapeSettings settings);
}

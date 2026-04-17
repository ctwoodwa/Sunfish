using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Components.DataDisplay.Map;

/// <summary>
/// Internal adapter interface mediating between SunfishMap and a JS tile rendering engine.
/// The MapLibre adapter is the only v1 implementation; the boundary exists so a future
/// engine swap does not break consumers.
/// </summary>
internal interface IMapEngineAdapter : IAsyncDisposable
{
    /// <summary>
    /// Initialize the map engine inside the given container element.
    /// </summary>
    Task InitializeAsync(ElementReference container, MapInitOptions options, DotNetObjectReference<SunfishMap> dotNetRef);

    /// <summary>
    /// Update the map viewport (center and zoom).
    /// </summary>
    Task UpdateViewportAsync(double centerLat, double centerLng, double zoom);

    /// <summary>
    /// Add a layer to the map.
    /// </summary>
    Task AddLayerAsync(MapLayerDescriptor layer);

    /// <summary>
    /// Remove a layer from the map by its id.
    /// </summary>
    Task RemoveLayerAsync(string layerId);

    /// <summary>
    /// Update an existing layer on the map.
    /// </summary>
    Task UpdateLayerAsync(MapLayerDescriptor layer);
}

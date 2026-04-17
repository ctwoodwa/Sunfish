namespace Sunfish.Components.Blazor.Components.DataDisplay.Map;

/// <summary>
/// Interface for map components that accept child layer registrations via CascadingValue.
/// Implemented by <see cref="SunfishMap"/>.
/// </summary>
public interface IMapLayerHost
{
    /// <summary>Registers a layer with this host during OnInitialized.</summary>
    void RegisterLayer(MapLayer layer);

    /// <summary>Unregisters a layer from this host during Dispose.</summary>
    void UnregisterLayer(MapLayer layer);
}

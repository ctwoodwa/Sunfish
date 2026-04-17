using Sunfish.Components.Blazor.Components.DataDisplay.Map;
using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

public partial class SunfishMap : SunfishComponentBase, IAsyncDisposable, IMapLayerHost
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public MapCenter? Center { get; set; }
    [Parameter] public int Zoom { get; set; } = 5;
    [Parameter] public double MinZoom { get; set; } = 1;
    [Parameter] public double MaxZoom { get; set; } = 18;
    [Parameter] public string? Width { get; set; } = "100%";
    [Parameter] public string? Height { get; set; } = "400px";
    [Parameter] public string? StyleUrl { get; set; }
    [Parameter] public bool Zoomable { get; set; } = true;
    [Parameter] public bool Pannable { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<MapClickEventArgs> OnClick { get; set; }
    [Parameter] public EventCallback<MapMarkerClickEventArgs> OnMarkerClick { get; set; }
    [Parameter] public EventCallback<MapShapeClickEventArgs> OnShapeClick { get; set; }
    [Parameter] public EventCallback<MapZoomEndEventArgs> OnZoomEnd { get; set; }
    [Parameter] public EventCallback<MapPanEndEventArgs> OnPanEnd { get; set; }
    [Parameter] public List<MapMarker> Markers { get; set; } = new();

    private string _containerId = $"mar-map-{Guid.NewGuid():N}";
    private ElementReference _containerRef;
    private IMapEngineAdapter? _adapter;
    private DotNetObjectReference<SunfishMap>? _dotNetRef;
    private bool _adapterInitialized;
    private readonly List<MapLayer> _layers = new();
    private readonly Dictionary<string, MapLayerDescriptor> _previousLayerDescriptors = new();

    void IMapLayerHost.RegisterLayer(MapLayer layer)
    {
        if (!_layers.Contains(layer))
        {
            _layers.Add(layer);
            InvokeAsync(StateHasChanged);
        }
    }

    void IMapLayerHost.UnregisterLayer(MapLayer layer)
    {
        if (_layers.Remove(layer))
        {
            InvokeAsync(StateHasChanged);
        }
    }

    internal IReadOnlyList<MapLayer> RegisteredLayers => _layers;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _adapter = new MapLibreAdapter(JS);
                _dotNetRef = DotNetObjectReference.Create(this);
                var initOptions = new MapInitOptions(
                    CenterLat: Center?.Latitude ?? 0,
                    CenterLng: Center?.Longitude ?? 0,
                    Zoom: Zoom,
                    MinZoom: MinZoom,
                    MaxZoom: MaxZoom,
                    Zoomable: Zoomable,
                    Pannable: Pannable);
                await _adapter.InitializeAsync(_containerRef, initOptions, _dotNetRef);
                _adapterInitialized = true;
                await SyncLayersToAdapterAsync();
            }
            catch (JSException) { }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_adapterInitialized) await SyncLayersToAdapterAsync();
    }

    private async Task SyncLayersToAdapterAsync()
    {
        if (_adapter is null) return;
        var currentDescriptors = new Dictionary<string, MapLayerDescriptor>();
        foreach (var layer in _layers)
        {
            var descriptor = layer.ToDescriptor();
            currentDescriptors[descriptor.Id] = descriptor;
        }
        foreach (var prevId in _previousLayerDescriptors.Keys)
        {
            if (!currentDescriptors.ContainsKey(prevId))
            {
                try { await _adapter.RemoveLayerAsync(prevId); } catch (JSException) { }
            }
        }
        foreach (var (id, descriptor) in currentDescriptors)
        {
            if (!_previousLayerDescriptors.TryGetValue(id, out var prev))
            {
                try { await _adapter.AddLayerAsync(descriptor); } catch (JSException) { }
            }
            else if (!prev.Equals(descriptor))
            {
                try { await _adapter.UpdateLayerAsync(descriptor); } catch (JSException) { }
            }
        }
        _previousLayerDescriptors.Clear();
        foreach (var (id, descriptor) in currentDescriptors)
            _previousLayerDescriptors[id] = descriptor;
    }

    /// <summary>
    /// Re-syncs all layers to the adapter. Call after making programmatic changes
    /// that do not trigger automatic re-rendering.
    /// </summary>
    public async Task Refresh()
    {
        if (_adapterInitialized)
        {
            await SyncLayersToAdapterAsync();
        }
    }

    [JSInvokable]
    public async Task OnMapClickFromJs(double latitude, double longitude)
    {
        var args = new MapClickEventArgs { Location = new MapLocation { Latitude = latitude, Longitude = longitude } };
        await OnClick.InvokeAsync(args);
    }

    [JSInvokable]
    public async Task OnMarkerClickFromJs(string title, double latitude, double longitude)
    {
        var args = new MapMarkerClickEventArgs { DataItem = new MapMarker { Title = title, Latitude = latitude, Longitude = longitude } };
        await OnMarkerClick.InvokeAsync(args);
    }

    [JSInvokable]
    public async Task OnShapeClickFromJs(string? dataItemJson)
    {
        await OnShapeClick.InvokeAsync(new MapShapeClickEventArgs());
    }

    [JSInvokable]
    public async Task OnZoomEndFromJs(double zoom, double centerLat, double centerLng)
    {
        var args = new MapZoomEndEventArgs { Zoom = zoom, Center = new MapCenter { Latitude = centerLat, Longitude = centerLng } };
        await OnZoomEnd.InvokeAsync(args);
    }

    [JSInvokable]
    public async Task OnPanEndFromJs(double centerLat, double centerLng)
    {
        var args = new MapPanEndEventArgs { Center = new MapCenter { Latitude = centerLat, Longitude = centerLng } };
        await OnPanEnd.InvokeAsync(args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_adapter is not null)
        {
            await _adapter.DisposeAsync();
            _adapter = null;
        }
        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _adapterInitialized = false;
    }
}

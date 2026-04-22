using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Media;

/// <summary>
/// Canonical MVP surface for the <c>map</c> catalog leaf (Tier 3 media family).
/// Renders a framed preview fallback client-side and defers the tile-serving + marker
/// rendering to a Leaflet-compatible JS module loaded from <see cref="JsModulePath"/>.
/// </summary>
/// <remarks>
/// MVP scope:
/// <list type="bullet">
///   <item>Public parameter surface: <c>Latitude</c>, <c>Longitude</c>, <c>Zoom</c>,
///     <c>Markers</c>, <c>TileUrl</c>, <c>Attribution</c>, <c>Width</c>, <c>Height</c>.</item>
///   <item>Events: <c>OnReady</c>, <c>OnMapClick</c>.</item>
///   <item>JSInterop call sites: <c>initMap(elementId, options, dotNetRef)</c>,
///     <c>addMarker</c>, <c>setView</c> — invoked from <see cref="JsModulePath"/>.</item>
/// </list>
/// Gaps (tracked for the "partial" → "implemented" flip):
/// <list type="bullet">
///   <item>The JS module file at <see cref="JsModulePath"/> is not yet shipped with this
///     package; in its absence the component renders the SVG-like preview fallback.</item>
///   <item>Marker icon styles other than <c>Default</c> are plumbed through options but
///     depend on the engine for visual rendering.</item>
/// </list>
/// </remarks>
public partial class SunfishMap : SunfishComponentBase, IAsyncDisposable
{
    /// <summary>Well-known module path for the Leaflet-compatible JS engine.</summary>
    /// <remarks>Not yet shipped with the package — see <see cref="SunfishMap"/> remarks.</remarks>
    public const string JsModulePath = "./_content/Sunfish.UIAdapters.Blazor/js/map.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Map-center latitude (-90 to 90).</summary>
    [Parameter] public double Latitude { get; set; }

    /// <summary>Map-center longitude (-180 to 180).</summary>
    [Parameter] public double Longitude { get; set; }

    /// <summary>Initial zoom level. Default 4.</summary>
    [Parameter] public int Zoom { get; set; } = 4;

    /// <summary>Markers rendered on the map.</summary>
    [Parameter] public List<MapMarker> Markers { get; set; } = new();

    /// <summary>Tile URL template. Default: OpenStreetMap.</summary>
    [Parameter] public string TileUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>HTML attribution string rendered in the lower-right corner.</summary>
    [Parameter] public string? Attribution { get; set; }
        = "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OpenStreetMap</a> contributors";

    /// <summary>CSS width of the map container.</summary>
    [Parameter] public string Width { get; set; } = "100%";

    /// <summary>CSS height of the map container.</summary>
    [Parameter] public string Height { get; set; } = "400px";

    /// <summary>Fired once the map engine has initialized.</summary>
    [Parameter] public EventCallback OnReady { get; set; }

    /// <summary>Fired when the user clicks on the map surface.</summary>
    [Parameter] public EventCallback<MapClickEventArgs> OnMapClick { get; set; }

    private readonly string _containerId = $"sf-map-{Guid.NewGuid():N}";
    private ElementReference _containerRef;
    private IJSObjectReference? _module;
    private IJSObjectReference? _mapHandle;
    private DotNetObjectReference<SunfishMap>? _dotNetRef;
    private bool _jsInitialized;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", JsModulePath);
            var options = new
            {
                latitude = Latitude,
                longitude = Longitude,
                zoom = Zoom,
                tileUrl = TileUrl,
                attribution = Attribution,
                markers = Markers.Select(m => new
                {
                    latitude = m.Latitude,
                    longitude = m.Longitude,
                    title = m.Title,
                    popup = m.Popup,
                    icon = m.Icon.ToString(),
                }).ToArray(),
            };
            _mapHandle = await _module.InvokeAsync<IJSObjectReference>("initMap", _containerId, options, _dotNetRef);
            _jsInitialized = true;
            StateHasChanged();
            await OnReady.InvokeAsync();
        }
        catch (JSException) { /* module not shipped yet — preview fallback remains visible */ }
        catch (InvalidOperationException) { /* prerendering or dispose race */ }
    }

    /// <summary>
    /// Re-centers the map on the supplied coordinate/zoom. No-op if the JS engine is not loaded.
    /// </summary>
    public async Task SetViewAsync(double latitude, double longitude, int? zoom = null)
    {
        if (_mapHandle is null) return;
        try { await _mapHandle.InvokeVoidAsync("setView", latitude, longitude, zoom ?? Zoom); }
        catch (JSException) { }
    }

    /// <summary>
    /// Adds a marker to the map. No-op if the JS engine is not loaded.
    /// </summary>
    public async Task AddMarkerAsync(MapMarker marker)
    {
        Markers.Add(marker);
        if (_mapHandle is null) return;
        try
        {
            await _mapHandle.InvokeVoidAsync("addMarker", new
            {
                latitude = marker.Latitude,
                longitude = marker.Longitude,
                title = marker.Title,
                popup = marker.Popup,
                icon = marker.Icon.ToString(),
            });
        }
        catch (JSException) { }
    }

    /// <summary>Invoked by the JS engine when the user clicks the map surface.</summary>
    [JSInvokable]
    public async Task OnMapClickFromJs(double latitude, double longitude)
    {
        await OnMapClick.InvokeAsync(new MapClickEventArgs { Latitude = latitude, Longitude = longitude });
    }

    /// <summary>Invoked by the JS engine when the user clicks an individual marker.</summary>
    [JSInvokable]
    public async Task OnMarkerClickFromJs(int index)
    {
        if (index < 0 || index >= Markers.Count) return;
        var marker = Markers[index];
        if (marker.OnClick.HasDelegate) await marker.OnClick.InvokeAsync(marker);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_mapHandle is not null) await _mapHandle.DisposeAsync();
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
        _dotNetRef?.Dispose();
    }
}
